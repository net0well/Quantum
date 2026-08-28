using Quantum.Audio.Devices;
using Quantum.Audio.Models;
using Quantum.Audio.Quality;
using Quantum.Audio.Spatial;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio.Profiles;

/// <summary>Resultado de um passo isolado da aplicação de um perfil.</summary>
public sealed record ProfileApplyStep(string Name, AudioResult Result);

/// <summary>Relatório completo — a interface mostra passo a passo o que pegou e o que não.</summary>
public sealed record ProfileApplyReport(AudioProfile Profile, IReadOnlyList<ProfileApplyStep> Steps)
{
    public bool AllSucceeded => Steps.All(s => s.Result.Success);

    public bool NeedsElevation => Steps.Any(s => !s.Result.Success && s.Result.RequiresElevation);

    public IEnumerable<ProfileApplyStep> Failures => Steps.Where(s => !s.Result.Success);

    public string Summary => AllSucceeded
        ? $"Perfil \"{Profile.Name}\" aplicado."
        : $"Perfil \"{Profile.Name}\" aplicado parcialmente ({Failures.Count()} de {Steps.Count} passos falharam).";
}

public interface IProfileApplier
{
    ProfileApplyReport Apply(AudioProfile profile, string deviceId);
}

/// <inheritdoc cref="IProfileApplier"/>
public sealed class ProfileApplier(
    IAudioDeviceService devices,
    IAudioQualityService quality,
    ISpatialAudioService spatial,
    ISystemAudioService system) : IProfileApplier
{
    public ProfileApplyReport Apply(AudioProfile profile, string deviceId)
    {
        var steps = new List<ProfileApplyStep>
        {
            new("Balanço dos canais", devices.SetBalance(deviceId, profile.Balance)),
        };

        if (profile.MasterVolume is { } master)
        {
            steps.Add(new ProfileApplyStep("Volume mestre", devices.SetMasterScalar(deviceId, master)));
        }

        if (profile.SpatialFormatId is { } spatialId)
        {
            steps.Add(new ProfileApplyStep("Áudio espacial", ApplySpatial(deviceId, spatialId)));
        }

        if (profile.Quality != QualityTarget.Keep)
        {
            steps.Add(new ProfileApplyStep("Qualidade", ApplyQuality(deviceId, profile.Quality)));
        }

        if (profile.Ducking is { } ducking)
        {
            steps.Add(new ProfileApplyStep("Comunicação", system.SetDuckingPreference(ducking)));
        }

        if (profile.Mono is { } mono)
        {
            steps.Add(new ProfileApplyStep("Áudio mono", system.SetMonoEnabled(mono)));
        }

        return new ProfileApplyReport(profile, steps);
    }

    private AudioResult ApplySpatial(string deviceId, uint spatialId)
    {
        var current = spatial.GetCurrentFormat(deviceId);
        if (current.Id == spatialId)
        {
            return AudioResult.Ok("Já estava no formato desejado.");
        }

        var target = spatial.GetFormats(deviceId).FirstOrDefault(f => f.Id == spatialId);
        if (target is null)
        {
            return AudioResult.Fail("O formato espacial deste perfil não está registrado neste dispositivo.");
        }

        return spatial.SetFormat(deviceId, target);
    }

    private AudioResult ApplyQuality(string deviceId, QualityTarget target)
    {
        var supported = quality.GetSupportedFormats(deviceId);
        if (supported.Count == 0)
        {
            return AudioResult.Fail("Não foi possível descobrir os formatos suportados.");
        }

        var chosen = target switch
        {
            QualityTarget.GameNative => supported
                .Where(f => f.IsGameNativeRate)
                .OrderByDescending(f => f.BitDepth)
                .FirstOrDefault(),
            QualityTarget.Highest => supported
                .OrderByDescending(f => f.SampleRate)
                .ThenByDescending(f => f.BitDepth)
                .FirstOrDefault(),
            _ => null,
        };

        if (chosen is null)
        {
            return AudioResult.Fail("Este dispositivo não oferece um formato compatível com o perfil.");
        }

        var current = quality.GetCurrentFormat(deviceId);
        return current == chosen
            ? AudioResult.Ok($"Já estava em {chosen.ShortLabel}.")
            : quality.SetFormat(deviceId, chosen);
    }
}
