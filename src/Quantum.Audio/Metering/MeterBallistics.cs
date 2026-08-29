namespace Quantum.Audio.Metering;

/// <summary>Como o ponteiro do medidor se move.</summary>
public sealed record MeterBallisticsOptions
{
    /// <summary>
    /// Quanto o nível cai por segundo quando o som some. 20 dB/s é o padrão de
    /// medidor de pico: rápido o bastante para acompanhar, lento o bastante para o
    /// olho ler.
    /// </summary>
    public double DecayDecibelsPerSecond { get; init; } = 20.0;

    /// <summary>Por quanto tempo o traço de pico fica parado antes de começar a cair.</summary>
    public TimeSpan PeakHoldDuration { get; init; } = TimeSpan.FromSeconds(1.5);

    /// <summary>Queda do traço de pico depois que a espera acaba.</summary>
    public double PeakDecayDecibelsPerSecond { get; init; } = 12.0;

    public static MeterBallisticsOptions Default { get; } = new();
}

/// <summary>
/// A física do medidor: ataque imediato, decaimento suave e traço de pico que segura.
/// </summary>
/// <remarks>
/// Sem isso a barra pisca a cada amostra e o resultado parece amador. Com isso, o
/// medidor se comporta como instrumento — e ainda permite amostrar a 30 Hz enquanto
/// a tela desenha a 60, porque o decaimento interpola entre as leituras.
///
/// O ataque é instantâneo de propósito: um medidor de pico existe para pegar o
/// transiente. Suavizar a subida esconderia justamente o que interessa.
/// </remarks>
public sealed class MeterBallistics(MeterBallisticsOptions? options = null)
{
    private readonly MeterBallisticsOptions _options = options ?? MeterBallisticsOptions.Default;

    private double _levelDecibels = AudioLevelScale.MinimumDecibels;
    private double _peakDecibels = AudioLevelScale.MinimumDecibels;
    private TimeSpan _holdRemaining;

    /// <summary>Nível atual, de 0 a 1, já na escala em dB.</summary>
    public double Level => AudioLevelScale.ToPosition(_levelDecibels);

    /// <summary>Posição do traço de pico, de 0 a 1.</summary>
    public double PeakHold => AudioLevelScale.ToPosition(_peakDecibels);

    public double LevelDecibels => _levelDecibels;

    public double PeakDecibels => _peakDecibels;

    /// <summary>Trava assim que o sinal encosta no teto; só sai quando reconhecido.</summary>
    public bool IsClipping { get; private set; }

    /// <summary>Alimenta o medidor com uma leitura nova.</summary>
    public void Push(double amplitude, TimeSpan elapsed)
    {
        var decibels = AudioLevelScale.ToDecibels(amplitude);
        var seconds = Math.Max(elapsed.TotalSeconds, 0);

        // Ataque imediato; na descida, cai no ritmo do decaimento.
        _levelDecibels = decibels >= _levelDecibels
            ? decibels
            : Math.Max(decibels, _levelDecibels - (_options.DecayDecibelsPerSecond * seconds));

        if (decibels >= _peakDecibels)
        {
            _peakDecibels = decibels;
            _holdRemaining = _options.PeakHoldDuration;
        }
        else if (_holdRemaining > TimeSpan.Zero)
        {
            _holdRemaining -= elapsed;
        }
        else
        {
            _peakDecibels = Math.Max(
                _levelDecibels,
                _peakDecibels - (_options.PeakDecayDecibelsPerSecond * seconds));
        }

        if (amplitude >= AudioLevelScale.ClipThreshold)
        {
            IsClipping = true;
        }
    }

    /// <summary>Apaga o indicador de clipping depois que o usuário o viu.</summary>
    public void ResetClipping() => IsClipping = false;

    /// <summary>Zera tudo — usado quando o dispositivo muda.</summary>
    public void Reset()
    {
        _levelDecibels = AudioLevelScale.MinimumDecibels;
        _peakDecibels = AudioLevelScale.MinimumDecibels;
        _holdRemaining = TimeSpan.Zero;
        IsClipping = false;
    }
}
