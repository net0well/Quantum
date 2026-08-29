using Quantum.Audio.Devices;
using Quantum.Audio.Models;
using Quantum.Audio.Spatial;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Profiles;

/// <summary>
/// Regras dos perfis: quais existem, o que pode ser alterado e como capturar o
/// estado atual. A gravação em si é do <see cref="IProfileRepository"/>.
/// </summary>
public sealed class ProfileService(
    IAudioVolumeController volumes,
    ISpatialAudioService spatial,
    ISystemAudioService system,
    IProfileRepository repository) : IProfileService
{
    private readonly Lock _gate = new();

    private List<AudioProfile>? _custom;

    public string StoragePath => repository.StoragePath;

    public IReadOnlyList<AudioProfile> GetProfiles()
    {
        lock (_gate)
        {
            _custom ??= [.. repository.Load()];
            return [.. BuiltInProfiles.All, .. _custom];
        }
    }

    public AudioResult Save(AudioProfile profile)
    {
        if (profile.IsBuiltIn)
        {
            return AudioResult.Fail("Perfis embutidos não podem ser sobrescritos. Duplique antes de editar.");
        }

        lock (_gate)
        {
            _custom ??= [.. repository.Load()];
            _custom.RemoveAll(p => p.Id == profile.Id);
            _custom.Add(profile);
            return repository.Save(_custom);
        }
    }

    public AudioResult Delete(string profileId)
    {
        lock (_gate)
        {
            _custom ??= [.. repository.Load()];

            return _custom.RemoveAll(p => p.Id == profileId) == 0
                ? AudioResult.Fail("Perfil não encontrado.")
                : repository.Save(_custom);
        }
    }

    public AudioProfile CaptureFromDevice(string deviceId, string name)
    {
        var volume = volumes.GetVolumeState(deviceId);

        return new AudioProfile
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = name,
            Description = "Capturado do estado atual do dispositivo.",
            IsBuiltIn = false,
            Balance = volume.Balance,
            MasterVolume = volume.MasterScalar,
            SpatialFormatId = spatial.GetCurrentFormat(deviceId).Id,
            Quality = QualityTarget.Keep,
            Ducking = system.GetDuckingPreference(),
            Mono = system.GetMonoEnabled(),
        };
    }
}
