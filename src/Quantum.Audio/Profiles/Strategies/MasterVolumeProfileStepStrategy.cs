using Quantum.Audio.Devices;
using Quantum.Audio.Models;

namespace Quantum.Audio.Profiles.Strategies;

/// <summary>Aplica o volume mestre, quando o perfil define um.</summary>
public sealed class MasterVolumeProfileStepStrategy(IAudioVolumeController volumes) : IProfileStepStrategy
{
    public int Order => 1;

    public string Name => "Volume mestre";

    public bool AppliesTo(AudioProfile profile) => profile.MasterVolume is not null;

    public AudioResult Apply(AudioProfile profile, string deviceId) =>
        profile.MasterVolume is { } master
            ? volumes.SetMasterScalar(deviceId, master)
            : AudioResult.Ok();
}
