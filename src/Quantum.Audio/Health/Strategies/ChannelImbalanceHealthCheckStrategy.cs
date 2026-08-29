using Quantum.Audio.Devices;
using Quantum.Audio.Models;

namespace Quantum.Audio.Health.Strategies;

/// <summary>
/// Procura canais desbalanceados — o problema que originou o Quantum: um lado do
/// fone quase mudo porque o balanço do Windows estava deslocado em 25 dB.
/// </summary>
public sealed class ChannelImbalanceHealthCheckStrategy(IAudioVolumeController volumes) : IHealthCheckStrategy
{
    /// <summary>Acima disso a diferença já é audível como "um lado mais baixo".</summary>
    internal const float ThresholdDecibels = 1.0f;

    public int Order => 0;

    public IEnumerable<HealthIssue> Inspect(IReadOnlyList<AudioDeviceInfo> devices)
    {
        foreach (var device in devices)
        {
            var volume = volumes.GetVolumeState(device.Id);
            if (volume.Channels.Count < 2)
            {
                continue;
            }

            var spread = volume.ChannelSpreadDecibels;
            if (spread <= ThresholdDecibels)
            {
                continue;
            }

            var quieter = volume.Channels.OrderBy(c => c.Decibels).First();

            yield return new HealthIssue(
                HealthIssueKind.ChannelImbalance,
                HealthSeverity.Critical,
                "Canais desbalanceados",
                $"O canal {quieter.Label.ToLowerInvariant()} está {spread:N1} dB abaixo do outro. " +
                "É isso que faz um lado do fone parecer quase mudo.",
                device.Id,
                device.ShortName);
        }
    }
}
