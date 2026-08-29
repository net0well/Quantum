using System.Runtime.InteropServices;
using Quantum.Audio.Interop;
using Quantum.Audio.Models;

namespace Quantum.Audio.Devices;

/// <inheritdoc cref="IAudioVolumeController"/>
public sealed class AudioVolumeController : IAudioVolumeController, IDisposable
{
    private static readonly Guid EventContext = new("7f3a91c2-4d18-4f6a-9c31-2b8e5d0a7f44");

    private readonly IMMDeviceEnumerator _enumerator;
    private readonly Lock _gate = new();
    private bool _disposed;

    public AudioVolumeController()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
    }

    public event EventHandler<string>? VolumeChanged;

    public VolumeState GetVolumeState(string deviceId)
    {
        lock (_gate)
        {
            return WithVolume(deviceId, volume =>
            {
                if (!HResults.Ok(volume.GetChannelCount(out var channelCount)))
                {
                    return VolumeState.Empty;
                }

                volume.GetMasterVolumeLevelScalar(out var masterScalar);
                volume.GetMasterVolumeLevel(out var masterDb);
                volume.GetMute(out var muted);
                volume.GetVolumeRange(out var minDb, out var maxDb, out var incrementDb);

                var channels = new List<ChannelLevel>((int)channelCount);
                for (uint c = 0; c < channelCount; c++)
                {
                    volume.GetChannelVolumeLevelScalar(c, out var scalar);
                    volume.GetChannelVolumeLevel(c, out var db);
                    channels.Add(new ChannelLevel((int)c, scalar, db));
                }

                return new VolumeState
                {
                    MasterScalar = masterScalar,
                    MasterDecibels = masterDb,
                    IsMuted = muted,
                    Range = new VolumeRange(minDb, maxDb, incrementDb),
                    Channels = channels,
                };
            }) ?? VolumeState.Empty;
        }
    }

    public AudioResult SetMasterScalar(string deviceId, float scalar)
    {
        var clamped = Math.Clamp(scalar, 0f, 1f);
        return Mutate(deviceId, volume =>
        {
            var context = EventContext;
            return volume.SetMasterVolumeLevelScalar(clamped, ref context);
        });
    }

    public AudioResult SetMasterDecibels(string deviceId, float decibels)
    {
        return Mutate(deviceId, volume =>
        {
            volume.GetVolumeRange(out var minDb, out var maxDb, out _);
            var context = EventContext;
            return volume.SetMasterVolumeLevel(Math.Clamp(decibels, minDb, maxDb), ref context);
        });
    }

    public AudioResult SetMuted(string deviceId, bool muted)
    {
        return Mutate(deviceId, volume =>
        {
            var context = EventContext;
            return volume.SetMute(muted, ref context);
        });
    }

    public AudioResult SetChannelScalar(string deviceId, int channelIndex, float scalar)
    {
        var clamped = Math.Clamp(scalar, 0f, 1f);
        return Mutate(deviceId, volume =>
        {
            if (!HResults.Ok(volume.GetChannelCount(out var count)) || channelIndex >= count)
            {
                return HResults.E_NOTFOUND;
            }

            var context = EventContext;
            return volume.SetChannelVolumeLevelScalar((uint)channelIndex, clamped, ref context);
        });
    }

    public AudioResult SetChannelDecibels(string deviceId, int channelIndex, float decibels)
    {
        return Mutate(deviceId, volume =>
        {
            if (!HResults.Ok(volume.GetChannelCount(out var count)) || channelIndex >= count)
            {
                return HResults.E_NOTFOUND;
            }

            volume.GetVolumeRange(out var minDb, out var maxDb, out _);
            var context = EventContext;
            return volume.SetChannelVolumeLevel((uint)channelIndex, Math.Clamp(decibels, minDb, maxDb), ref context);
        });
    }

    public AudioResult SetBalance(string deviceId, float balance)
    {
        var target = Math.Clamp(balance, -1f, 1f);
        return Mutate(deviceId, volume =>
        {
            if (!HResults.Ok(volume.GetChannelCount(out var count)) || count < 2)
            {
                return HResults.E_NOTFOUND;
            }

            // O canal mais alto define o volume "mestre"; o outro é atenuado.
            var loudest = 0f;
            for (uint c = 0; c < count; c++)
            {
                volume.GetChannelVolumeLevelScalar(c, out var scalar);
                loudest = MathF.Max(loudest, scalar);
            }

            var left = target > 0 ? loudest * (1f - target) : loudest;
            var right = target < 0 ? loudest * (1f + target) : loudest;

            var context = EventContext;
            var hr = volume.SetChannelVolumeLevelScalar(0, Math.Clamp(left, 0f, 1f), ref context);
            if (!HResults.Ok(hr))
            {
                return hr;
            }

            hr = volume.SetChannelVolumeLevelScalar(1, Math.Clamp(right, 0f, 1f), ref context);
            if (!HResults.Ok(hr))
            {
                return hr;
            }

            // Canais além de E/D (5.1, 7.1) acompanham o nível cheio.
            for (uint c = 2; c < count; c++)
            {
                volume.SetChannelVolumeLevelScalar(c, loudest, ref context);
            }

            return HResults.S_OK;
        });
    }

    public AudioResult CenterBalance(string deviceId) => SetBalance(deviceId, 0f);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        EndpointHelpers.Release(_enumerator);
    }

    private T? WithVolume<T>(string deviceId, Func<IAudioEndpointVolume, T> action)
    {
        var device = EndpointHelpers.Open(_enumerator, deviceId);
        if (device is null)
        {
            return default;
        }

        try
        {
            var volume = EndpointHelpers.Activate<IAudioEndpointVolume>(device, ComIids.AudioEndpointVolume);
            if (volume is null)
            {
                return default;
            }

            try
            {
                return action(volume);
            }
            finally
            {
                EndpointHelpers.Release(volume);
            }
        }
        finally
        {
            EndpointHelpers.Release(device);
        }
    }

    private AudioResult Mutate(string deviceId, Func<IAudioEndpointVolume, int> action)
    {
        lock (_gate)
        {
            int? hr;
            try
            {
                hr = WithVolume<int?>(deviceId, volume => action(volume));
            }
            catch (COMException ex)
            {
                return AudioResult.Fail(ex.HResult, "O dispositivo não respondeu. Ele pode ter sido desconectado.");
            }

            if (hr is null)
            {
                return AudioResult.Fail("Dispositivo não encontrado ou sem controle de volume.");
            }

            if (!HResults.Ok(hr.Value))
            {
                return AudioResult.Fail(hr.Value, $"O Windows recusou a alteração (0x{hr.Value:X8}).");
            }

            VolumeChanged?.Invoke(this, deviceId);
            return AudioResult.Ok();
        }
    }
}
