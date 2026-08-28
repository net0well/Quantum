using Quantum.Audio.Devices;
using Quantum.Audio.Models;
using Quantum.Audio.Profiles;
using Quantum.Audio.Spatial;
using Quantum.Audio.Storage;
using Quantum.Audio.SystemAudio;
using Xunit;

namespace Quantum.Audio.Tests;

public class ProfileTests : IDisposable
{
    private readonly IAppPaths _paths =
        new AppPaths(Path.Combine(Path.GetTempPath(), $"quantum-tests-{Guid.NewGuid():n}"));

    private ProfileService CreateService() =>
        new(new StubVolumes(), new StubSpatial(), new StubSystem(), _paths);

    [Fact]
    public void Perfil_de_fps_desliga_espacial_e_ducking()
    {
        var fps = BuiltInProfiles.Fps;

        Assert.Equal(0f, fps.Balance);
        Assert.Equal(SpatialFormatInfo.DisabledId, fps.SpatialFormatId);
        Assert.Equal(QualityTarget.GameNative, fps.Quality);
        Assert.Equal(DuckingPreference.DoNothing, fps.Ducking);
        Assert.False(fps.Mono);
    }

    [Fact]
    public void Perfil_de_filmes_liga_o_windows_sonic()
    {
        Assert.Equal(1u, BuiltInProfiles.Movies.SpatialFormatId);
        Assert.Equal(DuckingPreference.Reduce80, BuiltInProfiles.Movies.Ducking);
    }

    [Fact]
    public void Perfil_de_musica_busca_a_maior_qualidade()
    {
        Assert.Equal(QualityTarget.Highest, BuiltInProfiles.Music.Quality);
        Assert.Equal(SpatialFormatInfo.DisabledId, BuiltInProfiles.Music.SpatialFormatId);
    }

    [Fact]
    public void Todo_perfil_embutido_explica_as_escolhas()
    {
        Assert.All(BuiltInProfiles.All, profile =>
        {
            Assert.True(profile.IsBuiltIn);
            Assert.False(string.IsNullOrWhiteSpace(profile.Name));
            Assert.False(string.IsNullOrWhiteSpace(profile.Rationale));
        });
    }

    [Fact]
    public void Perfis_embutidos_sempre_aparecem_na_listagem()
    {
        var profiles = CreateService().GetProfiles();

        Assert.Equal(BuiltInProfiles.All.Count, profiles.Count);
    }

    [Fact]
    public void Perfil_personalizado_sobrevive_a_um_novo_carregamento()
    {
        var custom = new AudioProfile
        {
            Id = "meu-perfil",
            Name = "Meu perfil",
            Description = "Teste",
            Balance = -0.25f,
            MasterVolume = 0.8f,
            SpatialFormatId = 0,
            Ducking = DuckingPreference.DoNothing,
        };

        Assert.True(CreateService().Save(custom).Success);

        var reloaded = CreateService().GetProfiles().Single(p => p.Id == "meu-perfil");

        Assert.Equal("Meu perfil", reloaded.Name);
        Assert.Equal(-0.25f, reloaded.Balance);
        Assert.Equal(0.8f, reloaded.MasterVolume);
        Assert.Equal(DuckingPreference.DoNothing, reloaded.Ducking);
        Assert.False(reloaded.IsBuiltIn);
    }

    [Fact]
    public void Perfil_embutido_nao_pode_ser_sobrescrito()
    {
        var result = CreateService().Save(BuiltInProfiles.Fps);

        Assert.False(result.Success);
    }

    [Fact]
    public void Excluir_perfil_inexistente_falha_sem_estourar()
    {
        Assert.False(CreateService().Delete("nao-existe").Success);
    }

    [Fact]
    public void Duplicar_um_embutido_gera_um_perfil_editavel()
    {
        var copy = BuiltInProfiles.Fps.AsCustomCopy("FPS do Wellington");

        Assert.False(copy.IsBuiltIn);
        Assert.NotEqual(BuiltInProfiles.FpsId, copy.Id);
        Assert.Equal(BuiltInProfiles.Fps.SpatialFormatId, copy.SpatialFormatId);
    }

    public void Dispose()
    {
        if (Directory.Exists(_paths.Root))
        {
            Directory.Delete(_paths.Root, recursive: true);
        }
    }

    // ---- Dublês: a persistência de perfis não toca nos serviços de áudio ----
    // Só o controlador de volume aparece, porque CaptureFromDevice lê o estado atual.

    private sealed class StubVolumes : IAudioVolumeController
    {
        public event EventHandler<string>? VolumeChanged { add { } remove { } }

        public VolumeState GetVolumeState(string deviceId) => VolumeState.Empty;

        public AudioResult SetMasterScalar(string deviceId, float scalar) => AudioResult.Ok();

        public AudioResult SetMasterDecibels(string deviceId, float decibels) => AudioResult.Ok();

        public AudioResult SetMuted(string deviceId, bool muted) => AudioResult.Ok();

        public AudioResult SetChannelScalar(string deviceId, int channelIndex, float scalar) => AudioResult.Ok();

        public AudioResult SetChannelDecibels(string deviceId, int channelIndex, float decibels) => AudioResult.Ok();

        public AudioResult SetBalance(string deviceId, float balance) => AudioResult.Ok();

        public AudioResult CenterBalance(string deviceId) => AudioResult.Ok();
    }

    private sealed class StubSpatial : ISpatialAudioService
    {
        public IReadOnlyList<SpatialFormatInfo> GetFormats(string deviceId) => [SpatialFormatInfo.Disabled];

        public SpatialFormatInfo GetCurrentFormat(string deviceId) => SpatialFormatInfo.Disabled;

        public AudioResult SetFormat(string deviceId, SpatialFormatInfo format) => AudioResult.Ok();
    }

    private sealed class StubSystem : ISystemAudioService
    {
        public bool IsElevated => false;

        public DuckingPreference GetDuckingPreference() => DuckingPreference.DoNothing;

        public AudioResult SetDuckingPreference(DuckingPreference preference) => AudioResult.Ok();

        public bool GetMonoEnabled() => false;

        public AudioResult SetMonoEnabled(bool enabled) => AudioResult.Ok();

        public AudioResult RestartAudioService() => AudioResult.Ok();

        public bool RestartElevated() => false;

        public void OpenWindowsSoundSettings() { }

        public void OpenLegacySoundPanel() { }

        public void OpenDeviceManager() { }
    }
}
