namespace Quantum.Audio.Models;

/// <summary>Faixa de ganho suportada pelo endpoint, em decibéis.</summary>
public sealed record VolumeRange(float MinDecibels, float MaxDecibels, float IncrementDecibels);

/// <summary>Nível de um canal individual do endpoint.</summary>
public sealed record ChannelLevel(int Index, float Scalar, float Decibels)
{
    /// <summary>Rótulo do canal segundo a ordem padrão de WAVEFORMATEXTENSIBLE.</summary>
    public string Label => Index switch
    {
        0 => "Esquerda",
        1 => "Direita",
        2 => "Central",
        3 => "Subwoofer",
        4 => "Traseira esquerda",
        5 => "Traseira direita",
        6 => "Lateral esquerda",
        7 => "Lateral direita",
        _ => $"Canal {Index + 1}",
    };

    public string ShortLabel => Index switch
    {
        0 => "E",
        1 => "D",
        _ => (Index + 1).ToString(),
    };

    public float Percent => Scalar * 100f;
}

/// <summary>Estado completo de volume de um endpoint.</summary>
public sealed record VolumeState
{
    public required float MasterScalar { get; init; }
    public required float MasterDecibels { get; init; }
    public required bool IsMuted { get; init; }
    public required VolumeRange Range { get; init; }
    public required IReadOnlyList<ChannelLevel> Channels { get; init; }

    public static VolumeState Empty { get; } = new()
    {
        MasterScalar = 0,
        MasterDecibels = 0,
        IsMuted = false,
        Range = new VolumeRange(0, 0, 1),
        Channels = [],
    };

    /// <summary>
    /// Balanço de -1 (todo à esquerda) a +1 (todo à direita); 0 é centralizado.
    /// É o mesmo modelo do controle de balanço do Windows: o canal mais alto define
    /// o volume mestre e o outro é atenuado proporcionalmente.
    /// </summary>
    public float Balance
    {
        get
        {
            if (Channels.Count < 2)
            {
                return 0f;
            }

            var left = Channels[0].Scalar;
            var right = Channels[1].Scalar;
            var loudest = MathF.Max(left, right);

            return loudest <= 0.0001f ? 0f : (right - left) / loudest;
        }
    }

    public bool IsBalanced => MathF.Abs(Balance) < 0.005f;

    /// <summary>Diferença entre o canal mais alto e o mais baixo, em dB — o "quanto está torto".</summary>
    public float ChannelSpreadDecibels
    {
        get
        {
            if (Channels.Count < 2)
            {
                return 0f;
            }

            var max = Channels.Max(c => c.Decibels);
            var min = Channels.Min(c => c.Decibels);
            return max - min;
        }
    }
}
