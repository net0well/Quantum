using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using Quantum.Audio.Models;
using Quantum.Audio.Storage;

namespace Quantum.App.Services;

public interface IAppSettingsService
{
    AppSettings Current { get; }

    string StoragePath { get; }

    void Update(Func<AppSettings, AppSettings> change);

    bool GetStartWithWindows();

    AudioResult SetStartWithWindows(bool enabled);
}

/// <inheritdoc cref="IAppSettingsService"/>
public sealed class AppSettingsService : IAppSettingsService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "Quantum";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private AppSettings _current;

    public AppSettingsService(IAppPaths paths)
    {
        StoragePath = paths.SettingsFile;
        _current = Load();
    }

    public AppSettings Current => _current;

    public string StoragePath { get; }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        _current = change(_current);
        Persist();
    }

    public bool GetStartWithWindows()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string value && value.Length > 0;
    }

    public AudioResult SetStartWithWindows(bool enabled)
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            return AudioResult.Fail("Não foi possível descobrir o caminho do executável.");
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return AudioResult.Fail("Não foi possível abrir a chave de inicialização.");
            }

            if (enabled)
            {
                key.SetValue(RunValueName, $"\"{executable}\" --minimized", RegistryValueKind.String);
                return AudioResult.Ok("O Quantum vai iniciar junto com o Windows, minimizado na bandeja.");
            }

            key.DeleteValue(RunValueName, throwOnMissingValue: false);
            return AudioResult.Ok("O Quantum não inicia mais com o Windows.");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return AudioResult.Fail(ex.HResult, "Sem permissão para alterar a inicialização do Windows.");
        }
    }

    private AppSettings Load()
    {
        try
        {
            return File.Exists(StoragePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(StoragePath), JsonOptions)
                  ?? AppSettings.Default
                : AppSettings.Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(StoragePath, JsonSerializer.Serialize(_current, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Preferências são conveniência; falhar ao gravar não pode derrubar o app.
        }
    }
}
