namespace Quantum.Audio.Devices;

/// <summary>
/// Leitura de pico dos medidores.
/// </summary>
/// <remarks>
/// Existe separado do resto justamente por ser o caminho quente: é chamado dezenas de
/// vezes por segundo, para cada dispositivo e, no futuro, para cada sessão do mixer.
/// Abrir o endpoint e ativar a interface a cada leitura custava 815 µs medidos — com
/// dez sessões a 60 quadros por segundo, meio núcleo. Por isso aqui o ponteiro COM
/// fica vivo entre as chamadas.
/// </remarks>
public interface IAudioMeterService : IDisposable
{
    /// <summary>
    /// Escreve os picos (0 a 1) de cada canal em <paramref name="destination"/> e devolve
    /// quantos canais foram preenchidos. Zero significa dispositivo indisponível.
    /// O chamador fornece o buffer para não alocar a cada quadro.
    /// </summary>
    int Read(string deviceId, float[] destination);

    /// <summary>Descarta os ponteiros em cache — usar quando os dispositivos mudarem.</summary>
    void InvalidateAll();
}
