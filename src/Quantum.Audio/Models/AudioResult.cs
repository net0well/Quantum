using Quantum.Audio.Interop;

namespace Quantum.Audio.Models;

/// <summary>
/// Resultado de uma operação que altera o sistema. Boa parte das escritas de áudio
/// no Windows falha por falta de elevação, então o HRESULT é preservado para que a
/// interface possa oferecer a ação certa em vez de só dizer "erro".
/// </summary>
public readonly record struct AudioResult(bool Success, int HResult, string? Message)
{
    public static AudioResult Ok(string? message = null) => new(true, 0, message);

    public static AudioResult Fail(int hResult, string message) => new(false, hResult, message);

    public static AudioResult Fail(string message) => new(false, -1, message);

    /// <summary>A operação falhou por falta de privilégios de administrador.</summary>
    public bool RequiresElevation => HResult == HResults.E_ACCESSDENIED;

    public string DisplayMessage => Message ?? (Success ? "Concluído" : $"Falhou (0x{HResult:X8})");
}
