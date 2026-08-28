using Quantum.Audio.Models;

namespace Quantum.Audio.Profiles;

/// <summary>Guarda os perfis embutidos e os criados pelo usuário.</summary>
public interface IProfileService
{
    /// <summary>Embutidos primeiro, depois os personalizados.</summary>
    IReadOnlyList<AudioProfile> GetProfiles();

    AudioResult Save(AudioProfile profile);

    AudioResult Delete(string profileId);

    /// <summary>Cria um perfil a partir do estado atual de um dispositivo.</summary>
    AudioProfile CaptureFromDevice(string deviceId, string name);

    string StoragePath { get; }
}
