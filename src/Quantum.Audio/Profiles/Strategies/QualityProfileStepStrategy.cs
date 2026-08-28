using Quantum.Audio.Models;
using Quantum.Audio.Quality;

namespace Quantum.Audio.Profiles.Strategies;

/// <summary>
/// Escolhe o formato padrão conforme o alvo do perfil: a taxa nativa de jogo
/// (48 kHz) ou a maior que o hardware aceitar.
/// </summary>
public sealed class QualityProfileStepStrategy(IAudioQualityService quality) : IProfileStepStrategy
{
    public int Order => 3;

    public string Name => "Qualidade";

    public bool AppliesTo(AudioProfile profile) => profile.Quality != QualityTarget.Keep;

    public AudioResult Apply(AudioProfile profile, string deviceId)
    {
        var supported = quality.GetSupportedFormats(deviceId);
        if (supported.Count == 0)
        {
            return AudioResult.Fail("Não foi possível descobrir os formatos suportados.");
        }

        var chosen = profile.Quality switch
        {
            QualityTarget.GameNative => supported
                .Where(f => f.IsGameNativeRate)
                .OrderByDescending(f => f.BitDepth)
                .FirstOrDefault(),
            QualityTarget.Highest => supported
                .OrderByDescending(f => f.SampleRate)
                .ThenByDescending(f => f.BitDepth)
                .FirstOrDefault(),
            _ => null,
        };

        if (chosen is null)
        {
            return AudioResult.Fail("Este dispositivo não oferece um formato compatível com o perfil.");
        }

        return quality.GetCurrentFormat(deviceId) == chosen
            ? AudioResult.Ok($"Já estava em {chosen.ShortLabel}.")
            : quality.SetFormat(deviceId, chosen);
    }
}
