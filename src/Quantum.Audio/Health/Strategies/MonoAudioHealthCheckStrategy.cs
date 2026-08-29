using Quantum.Audio.Models;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Health.Strategies;

/// <summary>Áudio mono do Windows: sem estéreo não existe direção nenhuma.</summary>
public sealed class MonoAudioHealthCheckStrategy(ISystemAudioService system) : IHealthCheckStrategy
{
    public int Order => 4;

    public IEnumerable<HealthIssue> Inspect(IReadOnlyList<AudioDeviceInfo> devices)
    {
        if (!system.GetMonoEnabled())
        {
            yield break;
        }

        yield return new HealthIssue(
            HealthIssueKind.MonoEnabled,
            HealthSeverity.Warning,
            "Áudio mono ligado",
            "Os dois canais estão somados. Não existe direção nenhuma — em jogo, " +
            "é impossível saber de onde vem o som.");
    }
}
