namespace Quantum.Audio.Health;

public enum HealthSeverity
{
    Info,
    Warning,
    Critical,
}

public enum HealthIssueKind
{
    ChannelImbalance,
    DeviceMuted,
    VeryLowVolume,
    MonoEnabled,
    DuckingActive,
    SpatialOnForGaming,
}

/// <summary>Um problema encontrado pela verificação periódica.</summary>
public sealed record HealthIssue(
    HealthIssueKind Kind,
    HealthSeverity Severity,
    string Title,
    string Detail,
    string? DeviceId = null,
    string? DeviceName = null)
{
    /// <summary>Chave estável usada para não notificar o mesmo problema repetidamente.</summary>
    public string Signature => $"{Kind}|{DeviceId}";

    public string SeverityLabel => Severity switch
    {
        HealthSeverity.Critical => "CRÍTICO",
        HealthSeverity.Warning => "ATENÇÃO",
        _ => "INFO",
    };
}

/// <summary>Resultado completo de uma verificação.</summary>
public sealed record HealthReport(DateTime RunAt, IReadOnlyList<HealthIssue> Issues)
{
    public static HealthReport Empty { get; } = new(DateTime.MinValue, []);

    public bool IsHealthy => Issues.Count == 0;

    public int CriticalCount => Issues.Count(i => i.Severity == HealthSeverity.Critical);

    public int WarningCount => Issues.Count(i => i.Severity == HealthSeverity.Warning);

    public string Summary => Issues.Count == 0
        ? "Tudo certo — nenhum problema encontrado."
        : $"{Issues.Count} ponto(s) de atenção: {CriticalCount} crítico(s), {WarningCount} aviso(s).";
}
