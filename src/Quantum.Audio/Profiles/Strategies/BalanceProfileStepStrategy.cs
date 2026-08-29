using Quantum.Audio.Devices;
using Quantum.Audio.Models;

namespace Quantum.Audio.Profiles.Strategies;

/// <summary>Aplica o balanço do perfil. Sempre roda — todo perfil define um balanço.</summary>
public sealed class BalanceProfileStepStrategy(IAudioVolumeController volumes) : IProfileStepStrategy
{
    public int Order => 0;

    public string Name => "Balanço dos canais";

    public bool AppliesTo(AudioProfile profile) => true;

    public AudioResult Apply(AudioProfile profile, string deviceId) =>
        volumes.SetBalance(deviceId, profile.Balance);
}
