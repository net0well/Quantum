using Quantum.Audio.Devices;
using Quantum.Audio.Models;

namespace Quantum.Audio.Health.Strategies;

/// <summary>Dispositivo padrão silenciado — a causa mais boba de "o áudio parou".</summary>
public sealed class MutedDeviceHealthCheckStrategy(IAudioVolumeController volumes) : IHealthCheckStrategy
{
    public int Order => 1;

    public IEnumerable<HealthIssue> Inspect(IReadOnlyList<AudioDeviceInfo> devices)
    {
        foreach (var device in devices.Where(d => d.IsDefault))
        {
            if (!volumes.GetVolumeState(device.Id).IsMuted)
            {
                continue;
            }

            yield return new HealthIssue(
                HealthIssueKind.DeviceMuted,
                HealthSeverity.Warning,
                "Dispositivo padrão no mudo",
                $"\"{device.ShortName}\" é o dispositivo padrão e está silenciado.",
                device.Id,
                device.ShortName);
        }
    }
}
