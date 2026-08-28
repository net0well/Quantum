using Quantum.Audio.Models;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Profiles;

/// <summary>
/// Perfis que acompanham o Quantum. As escolhas seguem o que realmente muda o
/// resultado no Windows — e não o que apenas parece sofisticado.
/// </summary>
public static class BuiltInProfiles
{
    public const string FpsId = "builtin.fps";
    public const string MoviesId = "builtin.movies";
    public const string MusicId = "builtin.music";
    public const string VoiceId = "builtin.voice";

    public static AudioProfile Fps { get; } = new()
    {
        Id = FpsId,
        Name = "FPS competitivo",
        Description = "Máxima precisão posicional e nenhuma interferência do Windows.",
        Icon = "IconTarget",
        IsBuiltIn = true,
        Balance = 0f,
        SpatialFormatId = SpatialFormatInfo.DisabledId,
        Quality = QualityTarget.GameNative,
        Ducking = DuckingPreference.DoNothing,
        Mono = false,
        Rationale =
            "Balanço centralizado: qualquer desvio entre canais destrói a noção de direção.\n" +
            "Áudio espacial desligado: CS2, Valorant e Apex já aplicam HRTF próprio — " +
            "empilhar a virtualização do Windows por cima borra a imagem em vez de melhorá-la.\n" +
            "48 kHz: taxa nativa dos motores de jogo, sem reamostragem no caminho.\n" +
            "Ducking desligado: impede o Windows de abaixar o jogo em 80% quando você fala no Discord.",
    };

    public static AudioProfile Movies { get; } = new()
    {
        Id = MoviesId,
        Name = "Filmes e séries",
        Description = "Palco largo e envolvente para trilha e efeitos.",
        Icon = "IconFilm",
        IsBuiltIn = true,
        Balance = 0f,
        SpatialFormatId = 1, // Windows Sonic para Fones de Ouvido
        Quality = QualityTarget.GameNative,
        Ducking = DuckingPreference.Reduce80,
        Mono = false,
        Rationale =
            "Windows Sonic ligado: filmes vêm mixados em 5.1/7.1 e não trazem HRTF próprio, " +
            "então a virtualização do Windows tem o que espacializar — ao contrário do que acontece nos jogos.\n" +
            "48 kHz: taxa padrão da trilha de vídeo.\n" +
            "Ducking padrão: aqui é desejável que uma chamada abaixe o filme.",
    };

    public static AudioProfile Music { get; } = new()
    {
        Id = MusicId,
        Name = "Música",
        Description = "Sinal o mais limpo possível, sem processamento no meio.",
        Icon = "IconNote",
        IsBuiltIn = true,
        Balance = 0f,
        SpatialFormatId = SpatialFormatInfo.DisabledId,
        Quality = QualityTarget.Highest,
        Ducking = DuckingPreference.Reduce80,
        Mono = false,
        Rationale =
            "Áudio espacial desligado: a mixagem estéreo já foi decidida no estúdio; " +
            "virtualizar só afasta do que o produtor entregou.\n" +
            "Maior taxa e profundidade disponíveis para evitar reamostragem de material em alta resolução.",
    };

    public static AudioProfile Voice { get; } = new()
    {
        Id = VoiceId,
        Name = "Chamadas e reuniões",
        Description = "Voz inteligível, com o resto saindo da frente.",
        Icon = "IconMicrophone",
        IsBuiltIn = true,
        Balance = 0f,
        SpatialFormatId = SpatialFormatInfo.DisabledId,
        Quality = QualityTarget.GameNative,
        Ducking = DuckingPreference.Reduce80,
        Mono = false,
        Rationale =
            "Espacial desligado: voz é mono na origem, virtualizar não acrescenta nada e adiciona latência.\n" +
            "Ducking ativo: os outros sons saem do caminho assim que a chamada começa.",
    };

    public static IReadOnlyList<AudioProfile> All { get; } = [Fps, Movies, Music, Voice];
}
