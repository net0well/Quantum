namespace Quantum.Audio.Models;

/// <summary>
/// Um formato de áudio espacial registrado para o endpoint.
/// Id 0 significa "Desativado" (estéreo puro).
/// </summary>
public sealed record SpatialFormatInfo(uint Id, string Name, bool IsAvailable)
{
    public const uint DisabledId = 0;

    public static SpatialFormatInfo Disabled { get; } =
        new(DisabledId, "Desativado (estéreo puro)", true);

    public bool IsDisabled => Id == DisabledId;

    /// <summary>
    /// Formatos que exigem um app da Microsoft Store instalado e licenciado
    /// (Dolby Access, DTS Sound Unbound). Sem ele, selecionar não tem efeito.
    /// </summary>
    public string? Requirement => IsAvailable || IsDisabled
        ? null
        : "Requer o app correspondente da Microsoft Store";

    public override string ToString() => Name;
}
