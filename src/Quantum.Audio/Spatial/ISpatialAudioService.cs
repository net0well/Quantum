using Quantum.Audio.Models;

namespace Quantum.Audio.Spatial;

/// <summary>Leitura e troca do formato de áudio espacial de um endpoint.</summary>
public interface ISpatialAudioService
{
    /// <summary>Catálogo de formatos registrados, sempre começando por "Desativado".</summary>
    IReadOnlyList<SpatialFormatInfo> GetFormats(string deviceId);

    /// <summary>Formato ativo. Devolve "Desativado" quando não há espacialização.</summary>
    SpatialFormatInfo GetCurrentFormat(string deviceId);

    /// <summary>Troca o formato e confere se a mudança realmente pegou.</summary>
    AudioResult SetFormat(string deviceId, SpatialFormatInfo format);
}
