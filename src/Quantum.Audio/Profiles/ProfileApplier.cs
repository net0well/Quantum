using Quantum.Audio.Models;

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

/// <summary>
/// Executa os passos que o perfil define.
/// </summary>
/// <remarks>
/// Não sabe o que nenhum passo faz: só filtra os aplicáveis, ordena e coleta o
/// resultado de cada um. Toda a lógica mora nas implementações de
/// <see cref="IProfileStepStrategy"/>.
/// </remarks>
public sealed class ProfileApplier(IEnumerable<IProfileStepStrategy> strategies) : IProfileApplier
{
    private readonly IProfileStepStrategy[] _strategies = [.. strategies.OrderBy(s => s.Order)];

    public ProfileApplyReport Apply(AudioProfile profile, string deviceId)
    {
        var steps = _strategies
            .Where(strategy => strategy.AppliesTo(profile))
            .Select(strategy => new ProfileApplyStep(strategy.Name, strategy.Apply(profile, deviceId)))
            .ToList();

        return new ProfileApplyReport(profile, steps);
    }
}
