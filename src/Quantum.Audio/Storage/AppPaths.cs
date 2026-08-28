namespace Quantum.Audio.Storage;

/// <inheritdoc cref="IAppPaths"/>
public sealed class AppPaths : IAppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Quantum");
    }

    public string Root { get; }

    public string ProfilesFile => Path.Combine(Root, "profiles.json");

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string LogsFolder => Path.Combine(Root, "logs");

    public void EnsureCreated()
    {
        try
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(LogsFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sem poder gravar, o app ainda funciona — só não guarda perfis nem log.
        }
    }
}
