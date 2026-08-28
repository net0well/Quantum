using Quantum.Audio.Models;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Profiles.Strategies;

/// <summary>Define o que o Windows faz com os outros sons durante uma chamada.</summary>
public sealed class DuckingProfileStepStrategy(ISystemAudioService system) : IProfileStepStrategy
{
    public int Order => 4;

    public string Name => "Comunicação";

    public bool AppliesTo(AudioProfile profile) => profile.Ducking is not null;

    public AudioResult Apply(AudioProfile profile, string deviceId) =>
        profile.Ducking is { } ducking
            ? system.SetDuckingPreference(ducking)
            : AudioResult.Ok();
}
