namespace Quantum.Audio.Models;

/// <summary>
/// Um formato de mixagem em modo compartilhado — o que a caixa
/// "Formato padrão" das propriedades de som do Windows oferece.
/// </summary>
public sealed record AudioQualityFormat(int SampleRate, int BitDepth, int Channels)
{
    /// <summary>Rótulo no mesmo padrão do Windows.</summary>
    public string Label => $"{BitDepth} bits, {SampleRate} Hz ({QualityName})";

    public string ShortLabel => $"{BitDepth} bits · {SampleRate / 1000.0:0.#} kHz";

    public string QualityName => (BitDepth, SampleRate) switch
    {
        (16, 44100) => "Qualidade de CD",
        (16, 48000) => "Qualidade de DVD",
        (16, _) => "Qualidade de rádio",
        _ => "Qualidade de estúdio",
    };

    /// <summary>
    /// 48 kHz é a taxa nativa de praticamente todo motor de jogo. Bater com ela
    /// elimina uma etapa de reamostragem entre o jogo e a placa.
    /// </summary>
    public bool IsGameNativeRate => SampleRate == 48000;

    public override string ToString() => Label;
}
