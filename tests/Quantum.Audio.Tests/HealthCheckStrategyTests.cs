using Quantum.Audio.Devices;
using Quantum.Audio.Health;
using Quantum.Audio.Health.Strategies;
using Quantum.Audio.Models;
using Quantum.Audio.Spatial;
using Quantum.Audio.SystemAudio;
using Xunit;

namespace Quantum.Audio.Tests;

/// <summary>
/// Cada verificação virou uma classe própria, então dá para testar o julgamento
/// de cada uma sem COM, sem hardware e sem subir o monitor inteiro.
/// </summary>
public class HealthCheckStrategyTests
{
    private static AudioDeviceInfo Device(
        string id = "dev-1",
        bool isDefault = true,
        AudioFormFactor formFactor = AudioFormFactor.Headset,
        AudioDeviceKind kind = AudioDeviceKind.Output) => new()
    {
        Id = id,
        Kind = kind,
        FriendlyName = "Fone de teste",
        ShortName = "Fone de teste",
        State = AudioDeviceState.Active,
        FormFactor = formFactor,
        IsDefault = isDefault,
        ChannelCount = 2,
    };

    private static VolumeState Volume(float leftDb, float rightDb, bool muted = false, float master = 1f) => new()
    {
        MasterScalar = master,
        MasterDecibels = 0,
        IsMuted = muted,
        Range = new VolumeRange(-70, 5, 1),
        Channels =
        [
            new ChannelLevel(0, 1f, leftDb),
            new ChannelLevel(1, 1f, rightDb),
        ],
    };

    [Fact]
    public void Desbalanceio_acima_do_limite_vira_problema_critico()
    {
        // O caso real: esquerda +5, direita -20.
        var strategy = new ChannelImbalanceHealthCheckStrategy(new FakeVolumes(Volume(5f, -20f)));

        var issue = Assert.Single(strategy.Inspect([Device()]));

        Assert.Equal(HealthIssueKind.ChannelImbalance, issue.Kind);
        Assert.Equal(HealthSeverity.Critical, issue.Severity);
        Assert.Contains("direita", issue.Detail);
        Assert.Contains("25", issue.Detail);
    }

    [Fact]
    public void Diferenca_dentro_do_limite_nao_reclama()
    {
        var strategy = new ChannelImbalanceHealthCheckStrategy(new FakeVolumes(Volume(0f, -0.5f)));

        Assert.Empty(strategy.Inspect([Device()]));
    }

    [Fact]
    public void Canais_iguais_nao_reclamam()
    {
        var strategy = new ChannelImbalanceHealthCheckStrategy(new FakeVolumes(Volume(5f, 5f)));

        Assert.Empty(strategy.Inspect([Device()]));
    }

    [Fact]
    public void Dispositivo_padrao_no_mudo_e_sinalizado()
    {
        var strategy = new MutedDeviceHealthCheckStrategy(new FakeVolumes(Volume(0f, 0f, muted: true)));

        var issue = Assert.Single(strategy.Inspect([Device()]));

        Assert.Equal(HealthIssueKind.DeviceMuted, issue.Kind);
    }

    [Fact]
    public void Dispositivo_no_mudo_que_nao_e_o_padrao_e_ignorado()
    {
        var strategy = new MutedDeviceHealthCheckStrategy(new FakeVolumes(Volume(0f, 0f, muted: true)));

        Assert.Empty(strategy.Inspect([Device(isDefault: false)]));
    }

    [Fact]
    public void Volume_baixo_demais_vira_informativo()
    {
        var strategy = new LowVolumeHealthCheckStrategy(new FakeVolumes(Volume(0f, 0f, master: 0.05f)));

        var issue = Assert.Single(strategy.Inspect([Device()]));

        Assert.Equal(HealthSeverity.Info, issue.Severity);
    }

    [Fact]
    public void Volume_baixo_num_dispositivo_mudo_nao_duplica_o_aviso()
    {
        var strategy = new LowVolumeHealthCheckStrategy(
            new FakeVolumes(Volume(0f, 0f, muted: true, master: 0.05f)));

        Assert.Empty(strategy.Inspect([Device()]));
    }

    [Fact]
    public void Audio_mono_ligado_e_sinalizado()
    {
        var strategy = new MonoAudioHealthCheckStrategy(new FakeSystem(mono: true));

        Assert.Single(strategy.Inspect([]));
    }

    [Fact]
    public void Audio_mono_desligado_nao_reclama()
    {
        var strategy = new MonoAudioHealthCheckStrategy(new FakeSystem(mono: false));

        Assert.Empty(strategy.Inspect([]));
    }

    [Theory]
    [InlineData(DuckingPreference.Reduce80, true)]
    [InlineData(DuckingPreference.Reduce50, true)]
    [InlineData(DuckingPreference.MuteOthers, true)]
    [InlineData(DuckingPreference.DoNothing, false)]
    public void Ducking_so_reclama_quando_mexe_no_audio(DuckingPreference preference, bool expected)
    {
        var strategy = new DuckingHealthCheckStrategy(new FakeSystem(ducking: preference));

        Assert.Equal(expected, strategy.Inspect([]).Any());
    }

    [Fact]
    public void Espacial_ligado_em_fone_padrao_vira_aviso()
    {
        var format = new SpatialFormatInfo(1, "Windows Sonic", true);
        var strategy = new SpatialForGamingHealthCheckStrategy(new FakeSpatial(format));

        var issue = Assert.Single(strategy.Inspect([Device()]));

        Assert.Equal(HealthIssueKind.SpatialOnForGaming, issue.Kind);
        Assert.Contains("Windows Sonic", issue.Detail);
    }

    [Fact]
    public void Espacial_ligado_em_caixa_de_som_nao_interessa()
    {
        var strategy = new SpatialForGamingHealthCheckStrategy(
            new FakeSpatial(new SpatialFormatInfo(1, "Windows Sonic", true)));

        Assert.Empty(strategy.Inspect([Device(formFactor: AudioFormFactor.Speakers)]));
    }

    // ---- dublês ----

    private sealed class FakeVolumes(VolumeState state) : IAudioVolumeController
    {
        public event EventHandler<string>? VolumeChanged { add { } remove { } }

        public VolumeState GetVolumeState(string deviceId) => state;

        public AudioResult SetMasterScalar(string deviceId, float scalar) => AudioResult.Ok();

        public AudioResult SetMasterDecibels(string deviceId, float decibels) => AudioResult.Ok();

        public AudioResult SetMuted(string deviceId, bool muted) => AudioResult.Ok();

        public AudioResult SetChannelScalar(string deviceId, int channelIndex, float scalar) => AudioResult.Ok();

        public AudioResult SetChannelDecibels(string deviceId, int channelIndex, float decibels) => AudioResult.Ok();

        public AudioResult SetBalance(string deviceId, float balance) => AudioResult.Ok();

        public AudioResult CenterBalance(string deviceId) => AudioResult.Ok();
    }

    private sealed class FakeSpatial(SpatialFormatInfo current) : ISpatialAudioService
    {
        public IReadOnlyList<SpatialFormatInfo> GetFormats(string deviceId) => [current];

        public SpatialFormatInfo GetCurrentFormat(string deviceId) => current;

        public AudioResult SetFormat(string deviceId, SpatialFormatInfo format) => AudioResult.Ok();
    }

    private sealed class FakeSystem(
        bool mono = false,
        DuckingPreference ducking = DuckingPreference.DoNothing) : ISystemAudioService
    {
        public bool IsElevated => false;

        public DuckingPreference GetDuckingPreference() => ducking;

        public AudioResult SetDuckingPreference(DuckingPreference preference) => AudioResult.Ok();

        public bool GetMonoEnabled() => mono;

        public AudioResult SetMonoEnabled(bool enabled) => AudioResult.Ok();

        public AudioResult RestartAudioService() => AudioResult.Ok();

        public bool RestartElevated() => false;

        public void OpenWindowsSoundSettings() { }

        public void OpenLegacySoundPanel() { }

        public void OpenDeviceManager() { }
    }
}
