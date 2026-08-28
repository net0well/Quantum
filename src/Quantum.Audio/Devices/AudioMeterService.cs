using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Quantum.Audio.Interop;

namespace Quantum.Audio.Devices;

/// <inheritdoc cref="IAudioMeterService"/>
public sealed class AudioMeterService(ILogger<AudioMeterService> logger) : IAudioMeterService
{
    private readonly IMMDeviceEnumerator _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
    private readonly Dictionary<string, Meter> _meters = [];
    private readonly Lock _gate = new();

    private bool _disposed;

    public int Read(string deviceId, float[] destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        lock (_gate)
        {
            if (_disposed)
            {
                return 0;
            }

            if (!_meters.TryGetValue(deviceId, out var meter))
            {
                meter = Acquire(deviceId);
                if (meter is null)
                {
                    return 0;
                }

                _meters[deviceId] = meter;
            }

            var channels = Math.Min(meter.ChannelCount, destination.Length);
            if (channels == 0)
            {
                return 0;
            }

            try
            {
                // O array temporário do medidor tem o tamanho exato que o COM espera;
                // pedir menos canais que o dispositivo tem faz a chamada falhar.
                if (!HResults.Ok(meter.Interface.GetChannelsPeakValues(
                        (uint)meter.ChannelCount, meter.Buffer)))
                {
                    Drop(deviceId);
                    return 0;
                }
            }
            catch (COMException ex)
            {
                // Dispositivo removido no meio do caminho: solta o ponteiro e segue.
                logger.LogDebug(ex, "Medidor perdido para {DeviceId}.", deviceId);
                Drop(deviceId);
                return 0;
            }

            Array.Copy(meter.Buffer, destination, channels);
            return channels;
        }
    }

    public void InvalidateAll()
    {
        lock (_gate)
        {
            foreach (var meter in _meters.Values)
            {
                meter.Dispose();
            }

            _meters.Clear();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var meter in _meters.Values)
            {
                meter.Dispose();
            }

            _meters.Clear();
            EndpointHelpers.Release(_enumerator);
        }
    }

    private Meter? Acquire(string deviceId)
    {
        var device = EndpointHelpers.Open(_enumerator, deviceId);
        if (device is null)
        {
            return null;
        }

        try
        {
            var meter = EndpointHelpers.Activate<IAudioMeterInformation>(
                device, ComIids.AudioMeterInformation);

            if (meter is null)
            {
                return null;
            }

            if (!HResults.Ok(meter.GetMeteringChannelCount(out var channels)) || channels == 0)
            {
                EndpointHelpers.Release(meter);
                return null;
            }

            return new Meter(meter, (int)channels);
        }
        finally
        {
            EndpointHelpers.Release(device);
        }
    }

    private void Drop(string deviceId)
    {
        if (_meters.Remove(deviceId, out var meter))
        {
            meter.Dispose();
        }
    }

    /// <summary>Um ponteiro COM vivo mais o buffer que evita alocar a cada leitura.</summary>
    private sealed class Meter(IAudioMeterInformation meterInterface, int channelCount) : IDisposable
    {
        public IAudioMeterInformation Interface { get; } = meterInterface;

        public int ChannelCount { get; } = channelCount;

        public float[] Buffer { get; } = new float[channelCount];

        public void Dispose() => EndpointHelpers.Release(Interface);
    }
}
