using System.Collections.ObjectModel;
using Quantum.App.Mvvm;
using Quantum.Audio.Devices;
using Quantum.Audio.Drivers;
using Quantum.Audio.Models;
using Quantum.Audio.Quality;
using Quantum.Audio.Spatial;

namespace Quantum.App.ViewModels;

/// <summary>Um dispositivo de saída e todos os controles que valem só para ele.</summary>
public sealed class DeviceViewModel : ObservableObject
{
    private readonly IAudioVolumeController _volumes;
    private readonly IAudioMeterService _meters;
    private readonly IAudioQualityService _quality;
    private readonly ISpatialAudioService _spatial;
    private readonly IDriverService _drivers;

    /// <summary>Buffer reaproveitado: alocar um array por quadro viraria lixo a 30 Hz.</summary>
    private readonly float[] _peakBuffer = new float[8];

    private VolumeState _volume = VolumeState.Empty;
    private AudioQualityFormat? _selectedFormat;
    private SpatialFormatInfo? _selectedSpatialFormat;
    private AudioDriverInfo _driver = AudioDriverInfo.Unknown;
    private double _masterVolume;
    private double _balance;
    private bool _isMuted;
    private bool _suppress;
    private bool _detailsLoaded;

    public DeviceViewModel(
        AudioDeviceInfo info,
        IAudioVolumeController volumes,
        IAudioMeterService meters,
        IAudioQualityService quality,
        ISpatialAudioService spatial,
        IDriverService drivers)
    {
        Info = info;
        _volumes = volumes;
        _meters = meters;
        _quality = quality;
        _spatial = spatial;
        _drivers = drivers;

        RefreshVolume();
    }

    /// <summary>Avisa a janela principal sobre o resultado de uma alteração.</summary>
    public event EventHandler<AudioResult>? OperationCompleted;

    public AudioDeviceInfo Info { get; }

    public string Id => Info.Id;

    public string Name => Info.ShortName;

    public string Subtitle => Info.InterfaceName ?? Info.Connection ?? "Dispositivo de áudio";

    public bool IsConnected => Info.IsConnected;

    public bool IsDefault => Info.IsDefault;

    public bool IsOutput => Info.IsOutput;

    public bool IsInput => Info.IsInput;

    public string StateLabel => Info.StateLabel;

    public int ChannelCount => Info.ChannelCount;

    /// <summary>Chave da geometria declarada em Themes/Neon.xaml.</summary>
    public string Icon => Info.FormFactor switch
    {
        _ when Info.IsInput => "IconMicrophone",
        AudioFormFactor.Microphone => "IconMicrophone",
        AudioFormFactor.Headphones or AudioFormFactor.Headset => "IconHeadphones",
        AudioFormFactor.Speakers => "IconSpeaker",
        AudioFormFactor.DigitalAudioDisplayDevice => "IconMonitor",
        AudioFormFactor.SpdIf or AudioFormFactor.UnknownDigitalPassthrough => "IconDigital",
        _ => "IconAudio",
    };

    public ObservableCollection<ChannelViewModel> Channels { get; } = [];

    public ObservableCollection<AudioQualityFormat> Formats { get; } = [];

    public ObservableCollection<SpatialFormatInfo> SpatialFormats { get; } = [];

    /// <summary>Volume mestre em porcentagem (0–100).</summary>
    public double MasterVolume
    {
        get => _masterVolume;
        set
        {
            if (!SetProperty(ref _masterVolume, value) || _suppress)
            {
                return;
            }

            Report(_volumes.SetMasterScalar(Id, (float)(value / 100.0)));
            RefreshVolume();
        }
    }

    /// <summary>Balanço de -100 (esquerda) a +100 (direita).</summary>
    public double Balance
    {
        get => _balance;
        set
        {
            if (!SetProperty(ref _balance, value) || _suppress)
            {
                return;
            }

            Report(_volumes.SetBalance(Id, (float)(value / 100.0)));
            RefreshVolume();
        }
    }

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (!SetProperty(ref _isMuted, value) || _suppress)
            {
                return;
            }

            Report(_volumes.SetMuted(Id, value));
        }
    }

    public AudioQualityFormat? SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (!SetProperty(ref _selectedFormat, value) || _suppress || value is null)
            {
                return;
            }

            Report(_quality.SetFormat(Id, value));
        }
    }

    public SpatialFormatInfo? SelectedSpatialFormat
    {
        get => _selectedSpatialFormat;
        set
        {
            if (!SetProperty(ref _selectedSpatialFormat, value) || _suppress || value is null)
            {
                return;
            }

            Report(_spatial.SetFormat(Id, value));
            LoadSpatial();
        }
    }

    public AudioDriverInfo Driver
    {
        get => _driver;
        private set => SetProperty(ref _driver, value);
    }

    public string MasterDecibelsLabel => $"{_volume.MasterDecibels:+0.00;-0.00;0.00} dB";

    public string RangeLabel =>
        $"Faixa do dispositivo: {_volume.Range.MinDecibels:N1} a {_volume.Range.MaxDecibels:N1} dB";

    public bool IsBalanced => _volume.IsBalanced;

    public string BalanceLabel
    {
        get
        {
            if (_volume.Channels.Count < 2)
            {
                return "Dispositivo mono";
            }

            var offset = _volume.Balance;
            if (MathF.Abs(offset) < 0.005f)
            {
                return "Centralizado";
            }

            var side = offset < 0 ? "esquerda" : "direita";
            return $"Deslocado {Math.Abs(offset) * 100:N0}% para a {side}";
        }
    }

    public string SpreadLabel => _volume.ChannelSpreadDecibels < 0.01f
        ? "Canais idênticos"
        : $"Diferença de {_volume.ChannelSpreadDecibels:N2} dB entre os canais";

    /// <summary>Carrega formatos, espacial e driver — só na primeira seleção do dispositivo.</summary>
    public void EnsureDetailsLoaded()
    {
        if (_detailsLoaded || !IsConnected)
        {
            return;
        }

        _detailsLoaded = true;
        LoadFormats();

        // Áudio espacial só existe para saída.
        if (IsOutput)
        {
            LoadSpatial();
        }

        Driver = _drivers.GetDriverInfo(Info);
    }

    public void RefreshVolume()
    {
        if (!IsConnected)
        {
            return;
        }

        _volume = _volumes.GetVolumeState(Id);
        _suppress = true;

        try
        {
            _masterVolume = _volume.MasterScalar * 100.0;
            _balance = _volume.Balance * 100.0;
            _isMuted = _volume.IsMuted;

            SyncChannels();
        }
        finally
        {
            _suppress = false;
        }

        OnPropertyChanged(nameof(MasterVolume));
        OnPropertyChanged(nameof(Balance));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(MasterDecibelsLabel));
        OnPropertyChanged(nameof(RangeLabel));
        OnPropertyChanged(nameof(BalanceLabel));
        OnPropertyChanged(nameof(SpreadLabel));
        OnPropertyChanged(nameof(IsBalanced));
    }

    /// <summary>Atualiza os medidores de pico. Chamado pelo timer da janela.</summary>
    public void UpdateMeters()
    {
        if (!IsConnected || Channels.Count == 0)
        {
            return;
        }

        var channels = _meters.Read(Id, _peakBuffer);
        for (var i = 0; i < Channels.Count; i++)
        {
            Channels[i].Peak = i < channels ? _peakBuffer[i] : 0;
        }
    }

    private void SyncChannels()
    {
        if (Channels.Count != _volume.Channels.Count)
        {
            Channels.Clear();
            foreach (var level in _volume.Channels)
            {
                Channels.Add(new ChannelViewModel(level, ApplyChannelLevel));
            }

            return;
        }

        for (var i = 0; i < Channels.Count; i++)
        {
            Channels[i].Refresh(_volume.Channels[i]);
        }
    }

    private void ApplyChannelLevel(int index, double percent)
    {
        Report(_volumes.SetChannelScalar(Id, index, (float)(percent / 100.0)));
        RefreshVolume();
    }

    private void LoadFormats()
    {
        _suppress = true;

        try
        {
            Formats.Clear();
            foreach (var format in _quality.GetSupportedFormats(Id))
            {
                Formats.Add(format);
            }

            var current = _quality.GetCurrentFormat(Id);
            _selectedFormat = Formats.FirstOrDefault(f => f == current) ?? current;
        }
        finally
        {
            _suppress = false;
        }

        OnPropertyChanged(nameof(SelectedFormat));
    }

    private void LoadSpatial()
    {
        _suppress = true;

        try
        {
            SpatialFormats.Clear();
            foreach (var format in _spatial.GetFormats(Id))
            {
                SpatialFormats.Add(format);
            }

            var current = _spatial.GetCurrentFormat(Id);
            _selectedSpatialFormat = SpatialFormats.FirstOrDefault(f => f.Id == current.Id);
        }
        finally
        {
            _suppress = false;
        }

        OnPropertyChanged(nameof(SelectedSpatialFormat));
    }

    private void Report(AudioResult result) => OperationCompleted?.Invoke(this, result);
}
