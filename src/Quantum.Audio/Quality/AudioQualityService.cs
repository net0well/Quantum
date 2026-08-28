using System.Runtime.InteropServices;
using Quantum.Audio.Interop;
using Quantum.Audio.Models;

namespace Quantum.Audio.Quality;

/// <inheritdoc cref="IAudioQualityService"/>
public sealed class AudioQualityService : IAudioQualityService
{
    /// <summary>Taxas oferecidas pelo Windows na caixa "Formato padrão".</summary>
    private static readonly int[] CandidateSampleRates =
        [44100, 48000, 88200, 96000, 176400, 192000];

    private static readonly int[] CandidateBitDepths = [16, 24, 32];

    private readonly IMMDeviceEnumerator _enumerator;

    public AudioQualityService()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
    }

    public AudioQualityFormat? GetCurrentFormat(string deviceId)
    {
        var device = Open(deviceId);
        if (device is null)
        {
            return null;
        }

        try
        {
            var blob = DeviceProperties.GetBlob(device, PropertyKeys.AudioEngineDeviceFormat)
                       ?? DeviceProperties.GetBlob(device, PropertyKeys.AudioEngineOemFormat);

            if (blob is null)
            {
                return null;
            }

            var parsed = WaveFormatExtensible.Parse(blob);
            return parsed is null
                ? null
                : new AudioQualityFormat(parsed.Value.SampleRate, parsed.Value.BitsPerSample, parsed.Value.Channels);
        }
        finally
        {
            Release(device);
        }
    }

    public IReadOnlyList<AudioQualityFormat> GetSupportedFormats(string deviceId)
    {
        var device = Open(deviceId);
        if (device is null)
        {
            return [];
        }

        try
        {
            var channels = GetChannelCount(device);
            var client = ActivateClient(device);
            if (client is null)
            {
                return FallbackFormats(deviceId, channels);
            }

            try
            {
                var supported = new List<AudioQualityFormat>();
                foreach (var rate in CandidateSampleRates)
                {
                    foreach (var bits in CandidateBitDepths)
                    {
                        if (IsSupported(client, rate, bits, channels))
                        {
                            supported.Add(new AudioQualityFormat(rate, bits, channels));
                        }
                    }
                }

                // Garante que o formato atualmente ativo apareça na lista mesmo que
                // o hardware recuse a sondagem em modo exclusivo.
                var current = GetCurrentFormat(deviceId);
                if (current is not null && !supported.Contains(current))
                {
                    supported.Add(current);
                }

                return supported.Count == 0
                    ? FallbackFormats(deviceId, channels)
                    : [.. supported.OrderByDescending(f => f.SampleRate).ThenByDescending(f => f.BitDepth)];
            }
            finally
            {
                Release(client);
            }
        }
        finally
        {
            Release(device);
        }
    }

    public AudioResult SetFormat(string deviceId, AudioQualityFormat format)
    {
        var device = Open(deviceId);
        if (device is null)
        {
            return AudioResult.Fail("Dispositivo não encontrado.");
        }

        try
        {
            var wave = WaveFormatExtensible.CreatePcm(format.SampleRate, format.BitDepth, format.Channels);
            var blob = ToBytes(wave);

            var hr = DeviceProperties.SetBlob(device, PropertyKeys.AudioEngineDeviceFormat, blob);
            if (HResults.Ok(hr))
            {
                return AudioResult.Ok(
                    $"Formato definido para {format.ShortLabel}. Reconecte o dispositivo ou reinicie o serviço de áudio para valer.");
            }

            return hr == HResults.E_ACCESSDENIED
                ? AudioResult.Fail(hr, "Mudar o formato padrão exige executar o Quantum como administrador.")
                : AudioResult.Fail(hr, $"O Windows recusou o formato (0x{hr:X8}).");
        }
        catch (COMException ex)
        {
            return AudioResult.Fail(ex.HResult, "Falha ao falar com o dispositivo.");
        }
        finally
        {
            Release(device);
        }
    }

    /// <summary>
    /// A sondagem é feita em modo exclusivo de propósito: em modo compartilhado o
    /// motor de áudio aceita quase tudo (convertendo por baixo dos panos), então
    /// só o modo exclusivo revela o que o hardware realmente suporta.
    /// </summary>
    private static bool IsSupported(IAudioClient client, int sampleRate, int bits, int channels)
    {
        var format = WaveFormatExtensible.CreatePcm(sampleRate, bits, channels);
        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<WaveFormatExtensible>());

        try
        {
            Marshal.StructureToPtr(format, buffer, false);
            var hr = client.IsFormatSupported(ShareMode.Exclusive, buffer, out var closest);

            if (closest != 0)
            {
                Marshal.FreeCoTaskMem(closest);
            }

            return hr == HResults.S_OK;
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>Quando a sondagem não funciona, oferece ao menos o formato atual e o de 48 kHz.</summary>
    private IReadOnlyList<AudioQualityFormat> FallbackFormats(string deviceId, int channels)
    {
        var current = GetCurrentFormat(deviceId);
        var formats = new List<AudioQualityFormat>
        {
            new(48000, 24, channels),
            new(48000, 16, channels),
        };

        if (current is not null && !formats.Contains(current))
        {
            formats.Insert(0, current);
        }

        return formats;
    }

    private static byte[] ToBytes(WaveFormatExtensible format)
    {
        var size = Marshal.SizeOf<WaveFormatExtensible>();
        var buffer = new byte[size];
        var handle = Marshal.AllocHGlobal(size);

        try
        {
            Marshal.StructureToPtr(format, handle, false);
            Marshal.Copy(handle, buffer, 0, size);
            return buffer;
        }
        finally
        {
            Marshal.FreeHGlobal(handle);
        }
    }

    private static int GetChannelCount(IMMDevice device)
    {
        var iid = ComIids.AudioEndpointVolume;
        if (!HResults.Ok(device.Activate(ref iid, ComIids.CLSCTX_ALL, 0, out var raw)) ||
            raw is not IAudioEndpointVolume volume)
        {
            return 2;
        }

        try
        {
            return HResults.Ok(volume.GetChannelCount(out var count)) && count > 0 ? (int)count : 2;
        }
        finally
        {
            Release(volume);
        }
    }

    private static IAudioClient? ActivateClient(IMMDevice device)
    {
        var iid = ComIids.AudioClient;
        return HResults.Ok(device.Activate(ref iid, ComIids.CLSCTX_ALL, 0, out var raw))
            ? raw as IAudioClient
            : null;
    }

    private IMMDevice? Open(string deviceId) =>
        HResults.Ok(_enumerator.GetDevice(deviceId, out var device)) ? device : null;

    private static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }
}
