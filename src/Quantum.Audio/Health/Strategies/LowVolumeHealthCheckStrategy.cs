using Quantum.Audio.Devices;
using Quantum.Audio.Models;

namespace Quantum.Audio.Health.Strategies;

/// <summary>Volume do dispositivo padrão tão baixo que parece defeito.</summary>
public sealed class LowVolumeHealthCheckStrategy(IAudioVolumeController volumes) : IHealthCheckStrategy
{
    internal const float Threshold = 0.10f;

    public int Order => 2;

    public IEnumerable<HealthIssue> Inspect(IReadOnlyList<AudioDeviceInfo> devices)
    {
        foreach (var device in devices.Where(d => d.IsDefault))
        {
            var volume = volumes.GetVolumeState(device.Id);
            if (volume.IsMuted || volume.MasterScalar >= Threshold)
            {
                continue;
            }

            yield return new HealthIssue(
                HealthIssueKind.VeryLowVolume,
                HealthSeverity.Info,
                "Volume muito baixo",
                $"\"{device.ShortName}\" está em {volume.MasterScalar * 100:N0}%.",
                device.Id,
                device.ShortName);
        }
    }
}
