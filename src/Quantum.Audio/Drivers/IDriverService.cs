using Quantum.Audio.Models;

namespace Quantum.Audio.Drivers;

/// <summary>Dados do driver por trás de um endpoint de áudio.</summary>
public interface IDriverService
{
    AudioDriverInfo GetDriverInfo(AudioDeviceInfo device);
}
