namespace Quantum.Audio.Storage;

/// <summary>
/// Onde o Quantum guarda o que precisa persistir. Existe para que nenhum serviço
/// monte caminho na mão — e para que os testes apontem tudo para uma pasta temporária.
/// </summary>
public interface IAppPaths
{
    /// <summary>Pasta raiz de dados do usuário.</summary>
    string Root { get; }

    string ProfilesFile { get; }

    string SettingsFile { get; }

    string LogsFolder { get; }

    /// <summary>Cria a pasta raiz se ainda não existir.</summary>
    void EnsureCreated();
}
