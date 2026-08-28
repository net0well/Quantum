using Quantum.Audio.Devices;
using Quantum.Audio.Models;
using Quantum.Audio.Spatial;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Health;

public interface IHealthMonitor
{
    HealthReport RunCheckup();
}

/// <summary>
/// Verificação periódica do áudio. Feita para ser barata: só leituras, nenhuma
/// escrita, nenhum stream de áudio aberto e nenhuma alocação relevante — pode rodar
/// de minuto em minuto sem aparecer no gerenciador de tarefas.
/// </summary>
public sealed class HealthMonitor(
    IAudioDeviceCatalog catalog,
    IAudioVolumeController volumes,
    ISpatialAudioService spatial,
    ISystemAudioService system) : IHealthMonitor
{
    /// <summary>Acima disso a diferença entre canais já é audível como "um lado mais baixo".</summary>
    private const float ImbalanceThresholdDb = 1.0f;

    private const float LowVolumeThreshold = 0.10f;

    public HealthReport RunCheckup()
    {
        var issues = new List<HealthIssue>();

        foreach (var kind in (AudioDeviceKind[])[AudioDeviceKind.Output, AudioDeviceKind.Input])
        {
            foreach (var device in catalog.GetDevices(kind))
            {
                InspectDevice(device, issues);
            }
        }

        InspectSystem(issues);

        return new HealthReport(DateTime.Now, issues);
    }

    private void InspectDevice(AudioDeviceInfo device, List<HealthIssue> issues)
    {
        var volume = volumes.GetVolumeState(device.Id);
        if (volume.Channels.Count == 0)
        {
            return;
        }

        var spread = volume.ChannelSpreadDecibels;
        if (spread > ImbalanceThresholdDb)
        {
            var quieter = volume.Channels.OrderBy(c => c.Decibels).First();
            issues.Add(new HealthIssue(
                HealthIssueKind.ChannelImbalance,
                HealthSeverity.Critical,
                "Canais desbalanceados",
                $"O canal {quieter.Label.ToLowerInvariant()} está {spread:N1} dB abaixo do outro. " +
                "É isso que faz um lado do fone parecer quase mudo.",
                device.Id,
                device.ShortName));
        }

        if (volume.IsMuted && device.IsDefault)
        {
            issues.Add(new HealthIssue(
                HealthIssueKind.DeviceMuted,
                HealthSeverity.Warning,
                "Dispositivo padrão no mudo",
                $"\"{device.ShortName}\" é o dispositivo padrão e está silenciado.",
                device.Id,
                device.ShortName));
        }

        if (device.IsDefault && !volume.IsMuted && volume.MasterScalar < LowVolumeThreshold)
        {
            issues.Add(new HealthIssue(
                HealthIssueKind.VeryLowVolume,
                HealthSeverity.Info,
                "Volume muito baixo",
                $"\"{device.ShortName}\" está em {volume.MasterScalar * 100:N0}%.",
                device.Id,
                device.ShortName));
        }

        // Espacialização em fone é ótima para filme e ruim para FPS competitivo.
        if (device is { IsOutput: true, IsHeadphoneLike: true, IsDefault: true })
        {
            var current = spatial.GetCurrentFormat(device.Id);
            if (!current.IsDisabled)
            {
                issues.Add(new HealthIssue(
                    HealthIssueKind.SpatialOnForGaming,
                    HealthSeverity.Info,
                    "Áudio espacial ligado",
                    $"\"{current.Name}\" está ativo. Ótimo para filmes, mas atrapalha a " +
                    "precisão em FPS que já têm HRTF próprio.",
                    device.Id,
                    device.ShortName));
            }
        }
    }

    private void InspectSystem(List<HealthIssue> issues)
    {
        if (system.GetMonoEnabled())
        {
            issues.Add(new HealthIssue(
                HealthIssueKind.MonoEnabled,
                HealthSeverity.Warning,
                "Áudio mono ligado",
                "Os dois canais estão somados. Não existe direção nenhuma — em jogo, " +
                "é impossível saber de onde vem o som."));
        }

        var ducking = system.GetDuckingPreference();
        if (ducking != DuckingPreference.DoNothing)
        {
            var label = ducking switch
            {
                DuckingPreference.MuteOthers => "silenciar os outros sons",
                DuckingPreference.Reduce80 => "reduzir os outros sons em 80%",
                _ => "reduzir os outros sons em 50%",
            };

            issues.Add(new HealthIssue(
                HealthIssueKind.DuckingActive,
                HealthSeverity.Info,
                "Ducking de comunicação ativo",
                $"Ao detectar uma chamada, o Windows vai {label}. Em FPS isso derruba " +
                "o áudio do jogo justamente quando você está falando."));
        }
    }
}
