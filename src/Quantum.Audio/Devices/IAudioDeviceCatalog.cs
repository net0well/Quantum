using Quantum.Audio.Models;

namespace Quantum.Audio.Devices;

/// <summary>Quais dispositivos existem e o que se sabe sobre cada um.</summary>
public interface IAudioDeviceCatalog
{
    /// <summary>Disparado quando um dispositivo é conectado, removido ou muda de estado.</summary>
    event EventHandler? DevicesChanged;

    IReadOnlyList<AudioDeviceInfo> GetDevices(AudioDeviceKind kind, bool includeDisconnected = false);

    AudioDeviceInfo? GetDevice(string deviceId);
}
