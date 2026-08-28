using Quantum.Audio.Devices;
using Quantum.Audio.Models;

namespace Quantum.Audio.Health;

public interface IHealthMonitor
{
    HealthReport RunCheckup();
}

/// <summary>
/// Roda as verificações registradas e junta o resultado.
/// </summary>
/// <remarks>
/// Não sabe o que cada checagem faz — só as ordena e coleta. Toda a inteligência
/// mora nas implementações de <see cref="IHealthCheckStrategy"/>, então acrescentar
/// uma verificação nova nunca exige editar esta classe.
///
/// Continua barato: só leituras, nenhuma escrita, nenhum stream de áudio aberto.
/// </remarks>
public sealed class HealthMonitor(
    IAudioDeviceCatalog catalog,
    IEnumerable<IHealthCheckStrategy> strategies) : IHealthMonitor
{
    private readonly IHealthCheckStrategy[] _strategies = [.. strategies.OrderBy(s => s.Order)];

    public HealthReport RunCheckup()
    {
        List<AudioDeviceInfo> devices =
        [
            .. catalog.GetDevices(AudioDeviceKind.Output),
            .. catalog.GetDevices(AudioDeviceKind.Input),
        ];

        var issues = _strategies.SelectMany(strategy => strategy.Inspect(devices)).ToList();

        return new HealthReport(DateTime.Now, issues);
    }
}
