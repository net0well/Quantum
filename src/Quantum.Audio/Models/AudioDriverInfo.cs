namespace Quantum.Audio.Models;

/// <summary>Dados do driver que expõe o endpoint de áudio.</summary>
public sealed record AudioDriverInfo
{
    public string? Description { get; init; }
    public string? Provider { get; init; }
    public string? Version { get; init; }
    public DateTime? Date { get; init; }
    public string? InfName { get; init; }
    public string? InfSection { get; init; }
    public string? Service { get; init; }
    public string? InstanceId { get; init; }
    public string? Connection { get; init; }

    public static AudioDriverInfo Unknown { get; } = new() { Description = "Não identificado" };

    /// <summary>
    /// True quando o driver é o genérico da Microsoft (classe USB Audio ou HD Audio).
    /// Funciona bem, mas normalmente não expõe efeitos do fabricante.
    /// </summary>
    public bool IsGenericMicrosoftDriver =>
        Provider is not null && Provider.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

    public string DateLabel => Date?.ToString("dd/MM/yyyy") ?? "—";

    public string VersionLabel => string.IsNullOrWhiteSpace(Version) ? "—" : Version;
}
