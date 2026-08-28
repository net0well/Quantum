using Quantum.Audio.Models;

namespace Quantum.Audio.Quality;

/// <summary>Formato padrão (taxa de amostragem e profundidade de bits) do endpoint.</summary>
public interface IAudioQualityService
{
    /// <summary>Formato em uso hoje pelo motor de áudio compartilhado.</summary>
    AudioQualityFormat? GetCurrentFormat(string deviceId);

    /// <summary>Formatos que o hardware aceita, do mais alto para o mais baixo.</summary>
    IReadOnlyList<AudioQualityFormat> GetSupportedFormats(string deviceId);

    /// <summary>Define o formato padrão. Exige elevação e vale a partir da próxima inicialização do endpoint.</summary>
    AudioResult SetFormat(string deviceId, AudioQualityFormat format);
}
