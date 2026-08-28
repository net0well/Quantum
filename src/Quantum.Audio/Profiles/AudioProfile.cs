using Quantum.Audio.Models;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Profiles;

/// <summary>Como o perfil escolhe a taxa de amostragem e a profundidade de bits.</summary>
public enum QualityTarget
{
    /// <summary>Não mexe no formato atual.</summary>
    Keep,

    /// <summary>48 kHz — a taxa nativa de motores de jogo e de trilha de vídeo.</summary>
    GameNative,

    /// <summary>A maior taxa e profundidade que o hardware aceitar.</summary>
    Highest,
}

/// <summary>
/// Um conjunto de ajustes aplicável a um dispositivo de saída.
/// Todo campo opcional em <c>null</c> significa "deixa como está".
/// </summary>
public sealed record AudioProfile
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Chave do icone vetorial mostrado no cartao do perfil.</summary>
    public string Icon { get; init; } = "IconAudio";

    /// <summary>Balanço alvo de -1 a +1. O padrão centraliza.</summary>
    public float Balance { get; init; }

    /// <summary>Volume mestre alvo (0..1). Nulo mantém o volume atual.</summary>
    public float? MasterVolume { get; init; }

    /// <summary>Id do formato espacial; 0 desliga. Nulo mantém o atual.</summary>
    public uint? SpatialFormatId { get; init; }

    public QualityTarget Quality { get; init; } = QualityTarget.Keep;

    public DuckingPreference? Ducking { get; init; }

    public bool? Mono { get; init; }

    public bool IsBuiltIn { get; init; }

    /// <summary>Explicação das escolhas, mostrada na interface.</summary>
    public string? Rationale { get; init; }

    public AudioProfile AsCustomCopy(string newName) => this with
    {
        Id = Guid.NewGuid().ToString("n"),
        Name = newName,
        IsBuiltIn = false,
    };
}
