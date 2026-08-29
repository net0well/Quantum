using Quantum.Audio.Models;

namespace Quantum.Audio.Devices;

/// <summary>Volume, mudo, níveis por canal e balanço de um endpoint.</summary>
public interface IAudioVolumeController
{
    /// <summary>Disparado quando o volume de um endpoint muda por ação do Quantum.</summary>
    event EventHandler<string>? VolumeChanged;

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
}
