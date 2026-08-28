using System.Text.Json;
using System.Text.Json.Serialization;
using Quantum.Audio.Devices;
using Quantum.Audio.Models;
using Quantum.Audio.Spatial;
using Quantum.Audio.Storage;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Profiles;

/// <inheritdoc cref="IProfileService"/>
public sealed class ProfileService : IProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IAudioDeviceService _devices;
    private readonly ISpatialAudioService _spatial;
    private readonly ISystemAudioService _system;
    private readonly Lock _gate = new();

    private List<AudioProfile>? _custom;

    public ProfileService(
        IAudioDeviceService devices,
        ISpatialAudioService spatial,
        ISystemAudioService system,
        IAppPaths paths)
    {
        _devices = devices;
        _spatial = spatial;
        _system = system;
        StoragePath = paths.ProfilesFile;
    }

    public string StoragePath { get; }

    public IReadOnlyList<AudioProfile> GetProfiles()
    {
        lock (_gate)
        {
            _custom ??= Load();
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
            _custom ??= Load();
            _custom.RemoveAll(p => p.Id == profile.Id);
            _custom.Add(profile);
            return Persist();
        }
    }

    public AudioResult Delete(string profileId)
    {
        lock (_gate)
        {
            _custom ??= Load();
            return _custom.RemoveAll(p => p.Id == profileId) == 0
                ? AudioResult.Fail("Perfil não encontrado.")
                : Persist();
        }
    }

    public AudioProfile CaptureFromDevice(string deviceId, string name)
    {
        var volume = _devices.GetVolumeState(deviceId);
        var spatial = _spatial.GetCurrentFormat(deviceId);

        return new AudioProfile
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = name,
            Description = "Capturado do estado atual do dispositivo.",
            IsBuiltIn = false,
            Balance = volume.Balance,
            MasterVolume = volume.MasterScalar,
            SpatialFormatId = spatial.Id,
            Quality = QualityTarget.Keep,
            Ducking = _system.GetDuckingPreference(),
            Mono = _system.GetMonoEnabled(),
        };
    }

    private List<AudioProfile> Load()
    {
        try
        {
            if (!File.Exists(StoragePath))
            {
                return [];
            }

            var json = File.ReadAllText(StoragePath);
            var loaded = JsonSerializer.Deserialize<List<AudioProfile>>(json, JsonOptions);
            return loaded?.Where(p => !p.IsBuiltIn).ToList() ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Arquivo corrompido ou inacessível: começa vazio em vez de derrubar o app.
            return [];
        }
    }

    private AudioResult Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(StoragePath, JsonSerializer.Serialize(_custom, JsonOptions));
            return AudioResult.Ok("Perfil salvo.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return AudioResult.Fail(ex.HResult, $"Não foi possível gravar em {StoragePath}.");
        }
    }
}
