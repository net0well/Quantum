using System.Runtime.InteropServices;
using Quantum.Audio.Interop;
using Quantum.Audio.Models;

namespace Quantum.Audio.Devices;

/// <inheritdoc cref="IAudioDeviceCatalog"/>
public sealed class AudioDeviceCatalog : IAudioDeviceCatalog, IDisposable
{
    private readonly IMMDeviceEnumerator _enumerator;
    private readonly DeviceNotificationClient _notifications;
    private readonly Lock _gate = new();
    private bool _disposed;

    public AudioDeviceCatalog()
    {
        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
        _notifications = new DeviceNotificationClient(() => DevicesChanged?.Invoke(this, EventArgs.Empty));
        _enumerator.RegisterEndpointNotificationCallback(_notifications);
    }

    public event EventHandler? DevicesChanged;

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

                var defaultId = EndpointHelpers.GetDefaultId(_enumerator, flow, Role.Multimedia);
                var communicationsId = EndpointHelpers.GetDefaultId(_enumerator, flow, Role.Communications);

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
                        EndpointHelpers.Release(device);
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
                EndpointHelpers.Release(collection);
            }
        }
    }

    public AudioDeviceInfo? GetDevice(string deviceId)
    {
        lock (_gate)
        {
            var device = EndpointHelpers.Open(_enumerator, deviceId);
            if (device is null)
            {
                return null;
            }

            try
            {
                // O id não diz a direção, então tentamos as duas listas.
                foreach (var flow in (DataFlow[])[DataFlow.Render, DataFlow.Capture])
                {
                    var kind = flow == DataFlow.Capture ? AudioDeviceKind.Input : AudioDeviceKind.Output;
                    var info = Describe(device, kind,
                        EndpointHelpers.GetDefaultId(_enumerator, flow, Role.Multimedia),
                        EndpointHelpers.GetDefaultId(_enumerator, flow, Role.Communications));

                    if (info is not null)
                    {
                        return info;
                    }
                }

                return null;
            }
            finally
            {
                EndpointHelpers.Release(device);
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

        EndpointHelpers.Release(_enumerator);
    }

    private static AudioDeviceInfo? Describe(
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
        var volume = EndpointHelpers.Activate<IAudioEndpointVolume>(device, ComIids.AudioEndpointVolume);
        if (volume is not null)
        {
            if (HResults.Ok(volume.GetChannelCount(out var count)))
            {
                channelCount = (int)count;
            }

            EndpointHelpers.Release(volume);
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
