using Quantum.Audio.Models;

namespace Quantum.Audio.Devices;

/// <summary>Enumeração de endpoints de saída e controle de volume/balanço.</summary>
public interface IAudioDeviceService
{
    /// <summary>Disparado quando um dispositivo é conectado, removido ou muda de estado.</summary>
    event EventHandler? DevicesChanged;

    /// <summary>Disparado quando o volume de um endpoint muda por fora do Quantum.</summary>
    event EventHandler<string>? VolumeChanged;

    /// <summary>Endpoints de saída ou de entrada, conforme <paramref name="kind"/>.</summary>
    IReadOnlyList<AudioDeviceInfo> GetDevices(AudioDeviceKind kind, bool includeDisconnected = false);

    AudioDeviceInfo? GetDevice(string deviceId);

    VolumeState GetVolumeState(string deviceId);

    AudioResult SetMasterScalar(string deviceId, float scalar);

    AudioResult SetMasterDecibels(string deviceId, float decibels);

    AudioResult SetMuted(string deviceId, bool muted);

    AudioResult SetChannelScalar(string deviceId, int channelIndex, float scalar);

    AudioResult SetChannelDecibels(string deviceId, int channelIndex, float decibels);

    /// <summary>Balanço de -1 (esquerda) a +1 (direita); 0 centraliza.</summary>
    AudioResult SetBalance(string deviceId, float balance);

    /// <summary>Iguala todos os canais no nível do mais alto.</summary>
    AudioResult CenterBalance(string deviceId);

    /// <summary>Picos instantâneos por canal (0..1) para o medidor da interface.</summary>
    float[] GetChannelPeaks(string deviceId);
}
