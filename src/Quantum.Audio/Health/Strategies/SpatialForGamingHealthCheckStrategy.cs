using Quantum.Audio.Models;
using Quantum.Audio.Spatial;

namespace Quantum.Audio.Health.Strategies;

/// <summary>
/// Espacialização ligada em fone. Ótima para filme, atrapalha em FPS que já
/// aplica HRTF próprio — por isso é aviso, não erro.
/// </summary>
public sealed class SpatialForGamingHealthCheckStrategy(ISpatialAudioService spatial) : IHealthCheckStrategy
{
    public int Order => 3;

    public IEnumerable<HealthIssue> Inspect(IReadOnlyList<AudioDeviceInfo> devices)
    {
        var candidates = devices.Where(d => d is { IsOutput: true, IsHeadphoneLike: true, IsDefault: true });

        foreach (var device in candidates)
        {
            var current = spatial.GetCurrentFormat(device.Id);
            if (current.IsDisabled)
            {
                continue;
            }

            yield return new HealthIssue(
                HealthIssueKind.SpatialOnForGaming,
                HealthSeverity.Info,
                "Áudio espacial ligado",
                $"\"{current.Name}\" está ativo. Ótimo para filmes, mas atrapalha a " +
                "precisão em FPS que já têm HRTF próprio.",
                device.Id,
                device.ShortName);
        }
    }
}
