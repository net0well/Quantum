namespace Quantum.Audio.Models;

public enum AudioDeviceState
{
    Active,
    Disabled,
    NotPresent,
    Unplugged,
}

/// <summary>Corresponde ao enum EndpointFormFactor do mmdeviceapi.h.</summary>
public enum AudioFormFactor
{
    RemoteNetworkDevice = 0,
    Speakers = 1,
    LineLevel = 2,
    Headphones = 3,
    Microphone = 4,
    Headset = 5,
    Handset = 6,
    UnknownDigitalPassthrough = 7,
    SpdIf = 8,
    DigitalAudioDisplayDevice = 9,
    Unknown = 10,
}

/// <summary>Um endpoint de saída de áudio do Windows.</summary>
public sealed record AudioDeviceInfo
{
    public required string Id { get; init; }

    public required AudioDeviceKind Kind { get; init; }

    /// <summary>Nome completo: "Fone de ouvido do headset (HyperX Virtual Surround Sound)".</summary>
    public required string FriendlyName { get; init; }

    /// <summary>Nome curto editável pelo usuário: "Fone de ouvido do headset".</summary>
    public required string ShortName { get; init; }

    /// <summary>Nome do adaptador/placa: "HyperX Virtual Surround Sound".</summary>
    public string? InterfaceName { get; init; }

    public required AudioDeviceState State { get; init; }

    public AudioFormFactor FormFactor { get; init; } = AudioFormFactor.Unknown;

    /// <summary>Barramento: USB, HDAUDIO, BTHENUM...</summary>
    public string? Connection { get; init; }

    /// <summary>Caminho da instância PnP do hardware por trás do endpoint.</summary>
    public string? InstanceId { get; init; }

    public bool IsDefault { get; init; }

    public bool IsDefaultForCommunications { get; init; }

    public int ChannelCount { get; init; }

    public bool IsConnected => State == AudioDeviceState.Active;

    public bool IsOutput => Kind == AudioDeviceKind.Output;

    public bool IsInput => Kind == AudioDeviceKind.Input;

    /// <summary>True para fones/headsets, onde balanço e áudio espacial fazem mais diferença.</summary>
    public bool IsHeadphoneLike =>
        FormFactor is AudioFormFactor.Headphones or AudioFormFactor.Headset;

    public bool IsMicrophone =>
        Kind == AudioDeviceKind.Input ||
        FormFactor is AudioFormFactor.Microphone;

    public string StateLabel => State switch
    {
        AudioDeviceState.Active => "Conectado",
        AudioDeviceState.Disabled => "Desativado",
        AudioDeviceState.NotPresent => "Ausente",
        AudioDeviceState.Unplugged => "Desconectado",
        _ => "Desconhecido",
    };
}
