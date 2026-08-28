using Quantum.Audio.Models;

namespace Quantum.Audio.Health;

/// <summary>
/// Uma verificação isolada do áudio.
/// </summary>
/// <remarks>
/// Padrão Strategy: cada checagem é uma classe própria, testável sozinha, e
/// acrescentar uma nova não exige tocar no <see cref="HealthMonitor"/> — só
/// registrar mais uma implementação.
/// </remarks>
public interface IHealthCheckStrategy
{
    /// <summary>Ordem de exibição; menor aparece primeiro.</summary>
    int Order { get; }

    /// <summary>
    /// Inspeciona os dispositivos conectados e devolve o que encontrar de errado.
    /// Verificações de sistema ignoram a lista.
    /// </summary>
    IEnumerable<HealthIssue> Inspect(IReadOnlyList<AudioDeviceInfo> devices);
}
