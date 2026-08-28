using Quantum.Audio.Models;

namespace Quantum.Audio.SystemAudio;

/// <summary>O que o Windows faz com os outros sons quando detecta uma chamada de voz.</summary>
public enum DuckingPreference
{
    MuteOthers = 0,
    Reduce80 = 1,
    Reduce50 = 2,
    DoNothing = 3,
}

/// <summary>Configurações de áudio que valem para o sistema todo, não por dispositivo.</summary>
public interface ISystemAudioService
{
    bool IsElevated { get; }

    DuckingPreference GetDuckingPreference();

    AudioResult SetDuckingPreference(DuckingPreference preference);

    bool GetMonoEnabled();

    AudioResult SetMonoEnabled(bool enabled);

    /// <summary>Reinicia o serviço de áudio para aplicar mudanças de formato e espacialização.</summary>
    AudioResult RestartAudioService();

    /// <summary>Relança o Quantum pedindo elevação. Devolve false se o usuário recusar o UAC.</summary>
    bool RestartElevated();

    void OpenWindowsSoundSettings();

    void OpenLegacySoundPanel();

    void OpenDeviceManager();
}
