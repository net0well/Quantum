using System.Runtime.InteropServices;
using Quantum.Audio.Interop;
using Quantum.Audio.Models;

namespace Quantum.Audio.Devices;

/// <inheritdoc cref="IAudioDeviceService"/>
public sealed class AudioDeviceService : IAudioDeviceService, IDisposable
{
    private static readonly Guid EventContext = new("7f3a91c2-4d18-4f6a-9c31-2b8e5d0a7f44");

    private readonly IMMDeviceEnumerator _enumerator;
    private readonly DeviceNotificationClient _notifications;
    private readonly Lock _gate = new();
    private bool _disposed;

    public AudioDeviceService()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        _notifications = new DeviceNotificationClient(() => DevicesChanged?.Invoke(this, EventArgs.Empty));
        _enumerator.RegisterEndpointNotificationCallback(_notifications);
    }

    public event EventHandler? DevicesChanged;

    public event EventHandler<string>? VolumeChanged;

    public IReadOnlyList<AudioDeviceInfo> GetDevices(AudioDeviceKind kind, bool includeDisconnected = false)
    {
        lock (_gate)
        {
            var flow = kind == AudioDeviceKind.Input ? DataFlow.Capture : DataFlow.Render;
            var mask = includeDisconnected ? DeviceStateMask.All : DeviceStateMask.Active;
            if (!HResults.Ok(_enumerator.EnumAudioEndpoints(flow, mask, out var collection)))
            {
                return [];
            }

            try
            {
                if (!HResults.Ok(collection.GetCount(out var count)))
                {
                    return [];
                }

                var defaultId = GetDefaultId(flow, Role.Multimedia);
                var communicationsId = GetDefaultId(flow, Role.Communications);

                var devices = new List<AudioDeviceInfo>(count);
                for (var i = 0; i < count; i++)
                {
                    if (!HResults.Ok(collection.Item(i, out var device)))
                    {
                        continue;
                    }

                    try
                    {
                        var info = Describe(device, kind, defaultId, communicationsId);
                        if (info is not null)
                        {
                            devices.Add(info);
                        }
                    }
                    finally
                    {
                        Release(device);
                    }
                }

                return devices
                    .OrderByDescending(d => d.IsDefault)
                    .ThenByDescending(d => d.IsConnected)
                    .ThenBy(d => d.ShortName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            finally
            {
                Release(collection);
            }
        }
    }

    public AudioDeviceInfo? GetDevice(string deviceId)
    {
        lock (_gate)
        {
            var device = Open(deviceId);
            if (device is null)
            {
                return null;
            }

            try
            {
                // O id do endpoint não diz a direção, então tentamos as duas listas.
                foreach (var flow in (DataFlow[])[DataFlow.Render, DataFlow.Capture])
                {
                    var kind = flow == DataFlow.Capture ? AudioDeviceKind.Input : AudioDeviceKind.Output;
                    var info = Describe(device, kind,
                        GetDefaultId(flow, Role.Multimedia),
                        GetDefaultId(flow, Role.Communications));

                    if (info is not null)
                    {
                        return info;
                    }
                }

                return null;
            }
            finally
            {
                Release(device);
            }
        }
    }

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

    public float[] GetChannelPeaks(string deviceId)
    {
        lock (_gate)
        {
            var device = Open(deviceId);
            if (device is null)
            {
                return [];
            }

            try
            {
                var iid = ComIids.AudioMeterInformation;
                if (!HResults.Ok(device.Activate(ref iid, ComIids.CLSCTX_ALL, 0, out var raw)) ||
                    raw is not IAudioMeterInformation meter)
                {
                    return [];
                }

                try
                {
                    if (!HResults.Ok(meter.GetMeteringChannelCount(out var count)) || count == 0)
                    {
                        return [];
                    }

                    var peaks = new float[count];
                    return HResults.Ok(meter.GetChannelsPeakValues(count, peaks)) ? peaks : [];
                }
                finally
                {
                    Release(meter);
                }
            }
            finally
            {
                Release(device);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _enumerator.UnregisterEndpointNotificationCallback(_notifications);
        }
        catch (COMException)
        {
            // O serviço de áudio pode já ter sido derrubado; nada a fazer.
        }

        Release(_enumerator);
    }

    private AudioDeviceInfo? Describe(
        IMMDevice device,
        AudioDeviceKind kind,
        string? defaultId,
        string? communicationsId)
    {
        if (!HResults.Ok(device.GetId(out var id)) || string.IsNullOrEmpty(id))
        {
            return null;
        }

        device.GetState(out var rawState);

        var friendly = DeviceProperties.GetString(device, PropertyKeys.DeviceFriendlyName);
        var shortName = DeviceProperties.GetString(device, PropertyKeys.DeviceDescription);
        var interfaceName = DeviceProperties.GetString(device, PropertyKeys.DeviceInterfaceFriendlyName);
        var formFactor = DeviceProperties.GetUInt32(device, PropertyKeys.DeviceFormFactor);
        var instanceId = DeviceProperties.GetString(device, PropertyKeys.DeviceInstanceId);
        var connection = DeviceProperties.GetString(device, PropertyKeys.DeviceEnumeratorName);

        var channelCount = 0;
        var iid = ComIids.AudioEndpointVolume;
        if (HResults.Ok(device.Activate(ref iid, ComIids.CLSCTX_ALL, 0, out var raw)) &&
            raw is IAudioEndpointVolume volume)
        {
            if (HResults.Ok(volume.GetChannelCount(out var count)))
            {
                channelCount = (int)count;
            }

            Release(volume);
        }

        return new AudioDeviceInfo
        {
            Id = id,
            Kind = kind,
            FriendlyName = friendly ?? shortName ?? "Dispositivo de áudio",
            ShortName = shortName ?? friendly ?? "Dispositivo de áudio",
            InterfaceName = interfaceName,
            State = MapState(rawState),
            FormFactor = formFactor is { } ff && Enum.IsDefined(typeof(AudioFormFactor), (int)ff)
                ? (AudioFormFactor)ff
                : AudioFormFactor.Unknown,
            Connection = connection,
            InstanceId = NormalizeInstanceId(instanceId),
            IsDefault = string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase),
            IsDefaultForCommunications = string.Equals(id, communicationsId, StringComparison.OrdinalIgnoreCase),
            ChannelCount = channelCount,
        };
    }

    /// <summary>O Windows prefixa o caminho da instância com "{1}." no property store.</summary>
    internal static string? NormalizeInstanceId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var separator = raw.IndexOf('.');
        return separator >= 0 && raw.StartsWith('{') ? raw[(separator + 1)..] : raw;
    }

    internal static AudioDeviceState MapState(int rawState) => rawState switch
    {
        0x1 => AudioDeviceState.Active,
        0x2 => AudioDeviceState.Disabled,
        0x4 => AudioDeviceState.NotPresent,
        0x8 => AudioDeviceState.Unplugged,
        _ => AudioDeviceState.NotPresent,
    };

    private string? GetDefaultId(DataFlow flow, Role role)
    {
        if (!HResults.Ok(_enumerator.GetDefaultAudioEndpoint(flow, role, out var device)) ||
            device is null)
        {
            return null;
        }

        try
        {
            return HResults.Ok(device.GetId(out var id)) ? id : null;
        }
        finally
        {
            Release(device);
        }
    }

    private IMMDevice? Open(string deviceId)
    {
        return HResults.Ok(_enumerator.GetDevice(deviceId, out var device)) ? device : null;
    }

    private T? WithVolume<T>(string deviceId, Func<IAudioEndpointVolume, T> action)
    {
        var device = Open(deviceId);
        if (device is null)
        {
            return default;
        }

        try
        {
            var iid = ComIids.AudioEndpointVolume;
            if (!HResults.Ok(device.Activate(ref iid, ComIids.CLSCTX_ALL, 0, out var raw)) ||
                raw is not IAudioEndpointVolume volume)
            {
                return default;
            }

            try
            {
                return action(volume);
            }
            finally
            {
                Release(volume);
            }
        }
        finally
        {
            Release(device);
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

    private static void Release(object? comObject)
    {
        if (comObject is not null && Marshal.IsComObject(comObject))
        {
            Marshal.ReleaseComObject(comObject);
        }
    }

    /// <summary>Recebe as notificações de hot-plug do Windows em uma thread do COM.</summary>
    private sealed class DeviceNotificationClient(Action onChanged) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, int newState)
        {
            onChanged();
            return HResults.S_OK;
        }

        public int OnDeviceAdded(string deviceId)
        {
            onChanged();
            return HResults.S_OK;
        }

        public int OnDeviceRemoved(string deviceId)
        {
            onChanged();
            return HResults.S_OK;
        }

        public int OnDefaultDeviceChanged(DataFlow flow, Role role, string? defaultDeviceId)
        {
            onChanged();
            return HResults.S_OK;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => HResults.S_OK;
    }
}
