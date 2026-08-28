using Quantum.Audio.Models;
using Xunit;

namespace Quantum.Audio.Tests;

public class VolumeStateTests
{
    private static VolumeState WithChannels(params (float Scalar, float Decibels)[] channels) => new()
    {
        MasterScalar = channels.Max(c => c.Scalar),
        MasterDecibels = channels.Max(c => c.Decibels),
        IsMuted = false,
        Range = new VolumeRange(-70, 5, 1),
        Channels = [.. channels.Select((c, i) => new ChannelLevel(i, c.Scalar, c.Decibels))],
    };

    [Fact]
    public void Balance_com_canais_iguais_fica_centralizado()
    {
        var state = WithChannels((1.0f, 5f), (1.0f, 5f));

        Assert.Equal(0f, state.Balance, 4);
        Assert.True(state.IsBalanced);
    }

    [Fact]
    public void Balance_negativo_quando_a_direita_esta_mais_baixa()
    {
        // O caso real que originou o app: direita a 20,3% contra esquerda em 100%.
        var state = WithChannels((1.0f, 5f), (0.203f, -20f));

        Assert.True(state.Balance < 0);
        Assert.Equal(-0.797f, state.Balance, 3);
        Assert.False(state.IsBalanced);
    }

    [Fact]
    public void Balance_positivo_quando_a_esquerda_esta_mais_baixa()
    {
        var state = WithChannels((0.5f, -6f), (1.0f, 0f));

        Assert.Equal(0.5f, state.Balance, 3);
    }

    [Fact]
    public void ChannelSpread_mede_a_diferenca_em_decibeis()
    {
        var state = WithChannels((1.0f, 5f), (0.203f, -20f));

        Assert.Equal(25f, state.ChannelSpreadDecibels, 3);
    }

    [Fact]
    public void Dispositivo_mono_nunca_reporta_desbalanceio()
    {
        var state = WithChannels((0.6f, -8f));

        Assert.Equal(0f, state.Balance);
        Assert.Equal(0f, state.ChannelSpreadDecibels);
    }

    [Fact]
    public void Volume_zerado_nao_divide_por_zero()
    {
        var state = WithChannels((0f, -70f), (0f, -70f));

        Assert.Equal(0f, state.Balance);
    }

    [Theory]
    [InlineData(0, "Esquerda")]
    [InlineData(1, "Direita")]
    [InlineData(3, "Subwoofer")]
    [InlineData(9, "Canal 10")]
    public void Canais_recebem_o_rotulo_da_ordem_padrao(int index, string expected)
    {
        Assert.Equal(expected, new ChannelLevel(index, 1f, 0f).Label);
    }
}
