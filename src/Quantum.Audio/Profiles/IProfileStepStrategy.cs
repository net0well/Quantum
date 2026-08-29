using Quantum.Audio.Models;

namespace Quantum.Audio.Profiles;

/// <summary>
/// Um ajuste isolado que um perfil pode fazer.
/// </summary>
/// <remarks>
/// Padrão Strategy. Cada coisa que um perfil muda — balanço, volume, espacial,
/// qualidade, ducking, mono — é uma classe própria. O <see cref="ProfileApplier"/>
/// só as ordena e executa, então uma ação nova (ligar saída simultânea, trocar a
/// rota de um app) entra sem tocar em nada existente.
/// </remarks>
public interface IProfileStepStrategy
{
    /// <summary>Ordem de execução; menor primeiro.</summary>
    int Order { get; }

    /// <summary>Nome mostrado no relatório de aplicação.</summary>
    string Name { get; }

    /// <summary>False quando o perfil não define nada para este passo.</summary>
    bool AppliesTo(AudioProfile profile);

    AudioResult Apply(AudioProfile profile, string deviceId);
}
