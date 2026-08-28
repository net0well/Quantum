using Quantum.Audio.Models;
using Quantum.Audio.Spatial;

namespace Quantum.Audio.Profiles.Strategies;

/// <summary>Liga ou desliga a espacialização conforme o perfil.</summary>
public sealed class SpatialProfileStepStrategy(ISpatialAudioService spatial) : IProfileStepStrategy
{
    public int Order => 2;

    public string Name => "Áudio espacial";

    public bool AppliesTo(AudioProfile profile) => profile.SpatialFormatId is not null;

    public AudioResult Apply(AudioProfile profile, string deviceId)
    {
        if (profile.SpatialFormatId is not { } wanted)
        {
            return AudioResult.Ok();
        }

        var current = spatial.GetCurrentFormat(deviceId);
        if (current.Id == wanted)
        {
            return AudioResult.Ok("Já estava no formato desejado.");
        }

        var target = spatial.GetFormats(deviceId).FirstOrDefault(f => f.Id == wanted);

        return target is null
            ? AudioResult.Fail("O formato espacial deste perfil não está registrado neste dispositivo.")
            : spatial.SetFormat(deviceId, target);
    }
}
