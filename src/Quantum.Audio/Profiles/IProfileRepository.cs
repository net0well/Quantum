using Quantum.Audio.Models;

namespace Quantum.Audio.Profiles;

/// <summary>
/// Guarda e devolve os perfis criados pelo usuário.
/// </summary>
/// <remarks>
/// Padrão Repository: isola o meio de armazenamento de quem usa os perfis. As
/// regras e automações que virão depois precisam persistir também, e todas devem
/// entrar por aqui em vez de cada uma inventar o próprio jeito de ler um arquivo.
/// </remarks>
public interface IProfileRepository
{
    string StoragePath { get; }

    IReadOnlyList<AudioProfile> Load();

    AudioResult Save(IReadOnlyList<AudioProfile> profiles);
}
