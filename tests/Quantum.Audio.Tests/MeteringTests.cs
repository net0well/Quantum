using Quantum.Audio.Metering;
using Xunit;

namespace Quantum.Audio.Tests;

public class AudioLevelScaleTests
{
    [Fact]
    public void Amplitude_cheia_e_zero_dbfs()
    {
        Assert.Equal(0.0, AudioLevelScale.ToDecibels(1.0), 6);
    }

    [Fact]
    public void Metade_da_amplitude_e_menos_seis_db()
    {
        // A confusão clássica: 0,5 "parece" metade da barra, mas é −6 dB.
        Assert.Equal(-6.0206, AudioLevelScale.ToDecibels(0.5), 3);
    }

    [Fact]
    public void Silencio_vai_para_o_piso_da_regua()
    {
        Assert.Equal(AudioLevelScale.MinimumDecibels, AudioLevelScale.ToDecibels(0));
        Assert.Equal(AudioLevelScale.MinimumDecibels, AudioLevelScale.ToDecibels(-1));
    }

    [Fact]
    public void Amplitude_abaixo_do_piso_nao_estoura_a_regua()
    {
        Assert.Equal(AudioLevelScale.MinimumDecibels, AudioLevelScale.ToDecibels(0.0000001));
    }

    [Fact]
    public void Barra_usa_escala_em_db_e_nao_amplitude_linear()
    {
        // Se a barra fosse linear na amplitude, 0,5 daria 50%.
        // Na escala em dB, −6 dB numa régua de −60 a 0 fica a ~90%.
        var position = AudioLevelScale.AmplitudeToPosition(0.5);

        Assert.True(position > 0.85, $"esperado acima de 0,85 e veio {position:N3}");
        Assert.True(position < 0.95, $"esperado abaixo de 0,95 e veio {position:N3}");
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(-60.0, 0.0)]
    [InlineData(-30.0, 0.5)]
    public void Decibeis_viram_posicao_na_regua(double decibels, double expected)
    {
        Assert.Equal(expected, AudioLevelScale.ToPosition(decibels), 6);
    }

    [Fact]
    public void Regua_tem_as_marcacoes_de_medidor_profissional()
    {
        Assert.Equal<double[]>([-60, -40, -20, -12, -6, -3, 0], [.. AudioLevelScale.Ticks]);
    }
}

public class MeterBallisticsTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(33);

    [Fact]
    public void Ataque_e_imediato()
    {
        var meter = new MeterBallistics();

        meter.Push(1.0, Frame);

        Assert.Equal(0.0, meter.LevelDecibels, 6);
        Assert.Equal(1.0, meter.Level, 6);
    }

    [Fact]
    public void Decaimento_segue_a_taxa_configurada()
    {
        var meter = new MeterBallistics(new MeterBallisticsOptions { DecayDecibelsPerSecond = 20 });
        meter.Push(1.0, Frame);

        meter.Push(0.0, TimeSpan.FromSeconds(1));

        // Um segundo de silêncio a 20 dB/s: de 0 para −20 dB.
        Assert.Equal(-20.0, meter.LevelDecibels, 3);
    }

    [Fact]
    public void Decaimento_nunca_passa_do_sinal_atual()
    {
        var meter = new MeterBallistics();
        meter.Push(1.0, Frame);

        // Cai bastante, mas o sinal presente segura em −6 dB.
        meter.Push(0.5, TimeSpan.FromSeconds(10));

        Assert.Equal(-6.0206, meter.LevelDecibels, 3);
    }

    [Fact]
    public void Traco_de_pico_segura_enquanto_o_nivel_cai()
    {
        var meter = new MeterBallistics(new MeterBallisticsOptions
        {
            PeakHoldDuration = TimeSpan.FromSeconds(1.5),
        });

        meter.Push(1.0, Frame);
        meter.Push(0.001, TimeSpan.FromSeconds(0.5));

        Assert.Equal(0.0, meter.PeakDecibels, 3);
        Assert.True(meter.LevelDecibels < meter.PeakDecibels);
    }

    [Fact]
    public void Traco_de_pico_cai_depois_da_espera()
    {
        var meter = new MeterBallistics(new MeterBallisticsOptions
        {
            PeakHoldDuration = TimeSpan.FromSeconds(0.2),
            PeakDecayDecibelsPerSecond = 12,
        });

        meter.Push(1.0, Frame);
        meter.Push(0.001, TimeSpan.FromSeconds(0.3));   // consome a espera
        meter.Push(0.001, TimeSpan.FromSeconds(1.0));   // agora cai

        Assert.True(meter.PeakDecibels < 0.0, "o traço de pico deveria ter começado a cair");
    }

    [Fact]
    public void Clipping_trava_ate_ser_reconhecido()
    {
        var meter = new MeterBallistics();

        meter.Push(1.0, Frame);
        Assert.True(meter.IsClipping);

        // Mesmo com o som sumindo, o aviso continua.
        meter.Push(0.0, TimeSpan.FromSeconds(5));
        Assert.True(meter.IsClipping);

        meter.ResetClipping();
        Assert.False(meter.IsClipping);
    }

    [Fact]
    public void Sinal_normal_nao_marca_clipping()
    {
        var meter = new MeterBallistics();

        meter.Push(0.8, Frame);

        Assert.False(meter.IsClipping);
    }

    [Fact]
    public void Reset_zera_tudo()
    {
        var meter = new MeterBallistics();
        meter.Push(1.0, Frame);

        meter.Reset();

        Assert.Equal(0.0, meter.Level, 6);
        Assert.Equal(0.0, meter.PeakHold, 6);
        Assert.False(meter.IsClipping);
    }

    [Fact]
    public void Amostrar_a_30_hz_e_desenhar_a_60_nao_trava_a_barra()
    {
        // O truque que corta pela metade as chamadas COM: entre duas amostras,
        // o decaimento continua andando, então o desenho a 60 fps segue suave.
        var meter = new MeterBallistics();
        meter.Push(1.0, Frame);

        var first = meter.Level;
        meter.Push(0.0, TimeSpan.FromMilliseconds(16));
        var second = meter.Level;
        meter.Push(0.0, TimeSpan.FromMilliseconds(16));
        var third = meter.Level;

        Assert.True(second < first);
        Assert.True(third < second);
    }
}
