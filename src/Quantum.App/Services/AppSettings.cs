namespace Quantum.App.Services;

/// <summary>Preferências do próprio Quantum (não do áudio do Windows).</summary>
public sealed record AppSettings
{
    public bool MinimizeToTray { get; init; } = true;

    public bool StartMinimized { get; init; }

    public bool BackgroundCheckupEnabled { get; init; } = true;

    /// <summary>Intervalo entre verificações, em minutos.</summary>
    public int CheckupIntervalMinutes { get; init; } = 5;

    public bool NotifyOnIssues { get; init; } = true;

    public static AppSettings Default { get; } = new();

    /// <summary>Mantém o intervalo em uma faixa que não pesa nem deixa de ser útil.</summary>
    public int SafeIntervalMinutes => Math.Clamp(CheckupIntervalMinutes, 1, 120);
}
