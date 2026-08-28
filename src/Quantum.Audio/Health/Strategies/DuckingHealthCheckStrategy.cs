using Quantum.Audio.Models;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Health.Strategies;

/// <summary>
/// Ducking de comunicação ativo: ao detectar uma chamada, o Windows abaixa todo o
/// resto — em FPS, justamente quando você está falando com o time.
/// </summary>
public sealed class DuckingHealthCheckStrategy(ISystemAudioService system) : IHealthCheckStrategy
{
    public int Order => 5;

    public IEnumerable<HealthIssue> Inspect(IReadOnlyList<AudioDeviceInfo> devices)
    {
        var ducking = system.GetDuckingPreference();
        if (ducking == DuckingPreference.DoNothing)
        {
            yield break;
        }

        var label = ducking switch
        {
            DuckingPreference.MuteOthers => "silenciar os outros sons",
            DuckingPreference.Reduce80 => "reduzir os outros sons em 80%",
            _ => "reduzir os outros sons em 50%",
        };

        yield return new HealthIssue(
            HealthIssueKind.DuckingActive,
            HealthSeverity.Info,
            "Ducking de comunicação ativo",
            $"Ao detectar uma chamada, o Windows vai {label}. Em FPS isso derruba " +
            "o áudio do jogo justamente quando você está falando.");
    }
}
