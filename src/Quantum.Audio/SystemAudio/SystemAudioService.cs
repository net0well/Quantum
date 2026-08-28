using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using Quantum.Audio.Models;

namespace Quantum.Audio.SystemAudio;

/// <inheritdoc cref="ISystemAudioService"/>
public sealed class SystemAudioService : ISystemAudioService
{
    private const string AudioKeyPath = @"Software\Microsoft\Multimedia\Audio";
    private const string DuckingValue = "UserDuckingPreference";
    private const string MonoValue = "AccessibilityMonoMixState";

    private readonly Lazy<bool> _isElevated = new(DetectElevation);

    public bool IsElevated => _isElevated.Value;

    public DuckingPreference GetDuckingPreference()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AudioKeyPath);
        var raw = key?.GetValue(DuckingValue);

        // Sem valor gravado o Windows usa "reduzir 80%".
        return raw is int value && Enum.IsDefined(typeof(DuckingPreference), value)
            ? (DuckingPreference)value
            : DuckingPreference.Reduce80;
    }

    public AudioResult SetDuckingPreference(DuckingPreference preference) =>
        WriteUserValue(DuckingValue, (int)preference, preference switch
        {
            DuckingPreference.DoNothing => "O Windows não vai mais abaixar o jogo durante chamadas.",
            DuckingPreference.MuteOthers => "Outros sons serão silenciados durante chamadas.",
            _ => "Preferência de comunicação atualizada.",
        });

    public bool GetMonoEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AudioKeyPath);
        return key?.GetValue(MonoValue) is int value && value != 0;
    }

    public AudioResult SetMonoEnabled(bool enabled) =>
        WriteUserValue(MonoValue, enabled ? 1 : 0, enabled
            ? "Áudio mono ligado — os dois canais passam a tocar a mesma coisa."
            : "Áudio mono desligado — estéreo restaurado.");

    public AudioResult RestartAudioService()
    {
        if (!IsElevated)
        {
            return AudioResult.Fail(
                Interop.HResults.E_ACCESSDENIED,
                "Reiniciar o serviço de áudio exige executar o Quantum como administrador.");
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c net stop audiosrv /y & net start audiosrv",
                CreateNoWindow = true,
                UseShellExecute = false,
            });

            if (process is null)
            {
                return AudioResult.Fail("Não foi possível iniciar o comando.");
            }

            process.WaitForExit(30_000);
            return process.ExitCode == 0
                ? AudioResult.Ok("Serviço de áudio reiniciado.")
                : AudioResult.Fail($"O comando terminou com código {process.ExitCode}.");
        }
        catch (Win32Exception ex)
        {
            return AudioResult.Fail(ex.HResult, "Falha ao reiniciar o serviço de áudio.");
        }
    }

    public bool RestartElevated()
    {
        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                Verb = "runas",
            });

            return true;
        }
        catch (Win32Exception)
        {
            // 1223: o usuário cancelou o prompt do UAC.
            return false;
        }
    }

    public void OpenWindowsSoundSettings() => OpenShell("ms-settings:sound");

    public void OpenLegacySoundPanel() => OpenShell("control", "mmsys.cpl,,0");

    public void OpenDeviceManager() => OpenShell("devmgmt.msc");

    private static AudioResult WriteUserValue(string name, int value, string successMessage)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(AudioKeyPath, writable: true);
            if (key is null)
            {
                return AudioResult.Fail("Não foi possível abrir a chave de áudio do usuário.");
            }

            key.SetValue(name, value, RegistryValueKind.DWord);
            return AudioResult.Ok(successMessage);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return AudioResult.Fail(ex.HResult, "Sem permissão para gravar a configuração.");
        }
    }

    private static void OpenShell(string fileName, string? arguments = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = true,
            });
        }
        catch (Win32Exception)
        {
            // Se o shell recusar, não há alternativa útil — a interface segue funcionando.
        }
    }

    private static bool DetectElevation()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
