namespace Quantum.Audio.Metering;

/// <summary>
/// Converte amplitude em decibéis e em posição na régua do medidor.
/// </summary>
/// <remarks>
/// O detalhe que separa medidor de enfeite: os picos que a API de áudio devolve são
/// <b>amplitude linear de 0 a 1</b>, não decibéis. Uma barra que cresce linearmente
/// com a amplitude e exibe uma régua em dB está mentindo — metade da escala fica
/// espremida no topo. A conversão é 20·log10, e a posição na barra é calculada
/// sobre o valor em dB, não sobre a amplitude.
///
/// Exemplo do erro que isso evita: amplitude 0,5 parece "metade da barra", mas é
/// −6 dB, que numa régua de −60 a 0 fica a 90% do caminho.
/// </remarks>
public static class AudioLevelScale
{
    /// <summary>Piso da régua. Abaixo disso é silêncio para efeito visual.</summary>
    public const double MinimumDecibels = -60.0;

    /// <summary>Topo da régua: 0 dBFS, o limite digital.</summary>
    public const double MaximumDecibels = 0.0;

    /// <summary>Marcações desenhadas na régua, em dB.</summary>
    public static IReadOnlyList<double> Ticks { get; } = [-60, -40, -20, -12, -6, -3, 0];

    /// <summary>Acima disso o sinal entra na faixa de atenção.</summary>
    public const double WarningDecibels = -12.0;

    /// <summary>Acima disso está a um passo de estourar.</summary>
    public const double DangerDecibels = -3.0;

    /// <summary>Amplitude a partir da qual se considera clipping.</summary>
    public const double ClipThreshold = 0.99;

    /// <summary>Amplitude linear (0 a 1) para decibéis, limitado à régua.</summary>
    public static double ToDecibels(double amplitude)
    {
        if (amplitude <= 0)
        {
            return MinimumDecibels;
        }

        var decibels = 20.0 * Math.Log10(amplitude);
        return Math.Clamp(decibels, MinimumDecibels, MaximumDecibels);
    }

    /// <summary>Decibéis para posição de 0 a 1 na barra.</summary>
    public static double ToPosition(double decibels)
    {
        var position = (decibels - MinimumDecibels) / (MaximumDecibels - MinimumDecibels);
        return Math.Clamp(position, 0.0, 1.0);
    }

    /// <summary>Atalho de amplitude direto para posição na barra.</summary>
    public static double AmplitudeToPosition(double amplitude) => ToPosition(ToDecibels(amplitude));
}
