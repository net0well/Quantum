using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Quantum.App.Services;

/// <summary>
/// Devolve ao Windows a memória que o WPF reservou enquanto a janela estava aberta.
/// Sem isso um app de bandeja fica ocupando centenas de MB parado, à toa: as páginas
/// continuam alocadas mesmo sem ninguém olhando para a interface.
/// </summary>
internal static class MemoryTrimmer
{
    private static readonly Stopwatch SinceLastTrim = Stopwatch.StartNew();

    /// <summary>Evita repetir a limpeza em sequência — ela não é de graça.</summary>
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(20);

    private static bool _firstTrim = true;

    public static void Trim()
    {
        if (!_firstTrim && SinceLastTrim.Elapsed < MinimumInterval)
        {
            return;
        }

        _firstTrim = false;
        SinceLastTrim.Restart();

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        try
        {
            using var process = Process.GetCurrentProcess();
            EmptyWorkingSet(process.Handle);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
                                       or InvalidOperationException)
        {
            // Sem a API disponível, o GC acima já ajuda.
        }
    }

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(nint processHandle);
}
