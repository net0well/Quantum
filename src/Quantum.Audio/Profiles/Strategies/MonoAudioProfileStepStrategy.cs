using Quantum.Audio.Models;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Profiles.Strategies;

/// <summary>Liga ou desliga o áudio mono do Windows.</summary>
public sealed class MonoAudioProfileStepStrategy(ISystemAudioService system) : IProfileStepStrategy
{
    public int Order => 5;

    public string Name => "Áudio mono";

    public bool AppliesTo(AudioProfile profile) => profile.Mono is not null;

    public AudioResult Apply(AudioProfile profile, string deviceId) =>
        profile.Mono is { } mono
            ? system.SetMonoEnabled(mono)
            : AudioResult.Ok();
}
