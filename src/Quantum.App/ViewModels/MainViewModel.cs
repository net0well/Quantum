using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Quantum.App.Mvvm;
using Quantum.App.Services;
using Quantum.Audio.Devices;
using Quantum.Audio.Drivers;
using Quantum.Audio.Health;
using Quantum.Audio.Models;
using Quantum.Audio.Profiles;
using Quantum.Audio.Quality;
using Quantum.Audio.Spatial;
using Quantum.Audio.SystemAudio;

namespace Quantum.App.ViewModels;

/// <summary>Opção da caixa "quando o Windows detectar uma chamada".</summary>
public sealed record DuckingOption(DuckingPreference Value, string Label)
{
    public override string ToString() => Label;
}

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly IAudioDeviceCatalog _catalog;
    private readonly IAudioVolumeController _volumes;
    private readonly IAudioMeterService _meters;
    private readonly IAudioQualityService _quality;
    private readonly ISpatialAudioService _spatial;
    private readonly IDriverService _drivers;
    private readonly ISystemAudioService _system;
    private readonly IProfileService _profiles;
    private readonly IProfileApplier _applier;
    private readonly IHealthMonitor _health;
    private readonly IAppSettingsService _settings;

    private readonly DispatcherTimer _meterTimer;
    private readonly DispatcherTimer _checkupTimer;
    private readonly HashSet<string> _notifiedIssues = [];

    private DeviceViewModel? _selectedDevice;
    private AudioDeviceKind _selectedKind = AudioDeviceKind.Output;
    private DuckingOption? _selectedDucking;
    private HealthReport _lastReport = HealthReport.Empty;
    private string _statusMessage = "Pronto.";
    private bool _statusIsError;
    private bool _monoEnabled;
    private string _newProfileName = string.Empty;
    private bool _showDisconnected;
    private bool _metersActive;
    private bool _suppress;

    public MainViewModel(
        IAudioDeviceCatalog catalog,
        IAudioVolumeController volumes,
        IAudioMeterService meters,
        IAudioQualityService quality,
        ISpatialAudioService spatial,
        IDriverService drivers,
        ISystemAudioService system,
        IProfileService profiles,
        IProfileApplier applier,
        IHealthMonitor health,
        IAppSettingsService settings)
    {
        _catalog = catalog;
        _volumes = volumes;
        _meters = meters;
        _quality = quality;
        _spatial = spatial;
        _drivers = drivers;
        _system = system;
        _profiles = profiles;
        _applier = applier;
        _health = health;
        _settings = settings;

        DuckingOptions =
        [
            new DuckingOption(DuckingPreference.DoNothing, "Não fazer nada (recomendado para jogos)"),
            new DuckingOption(DuckingPreference.Reduce50, "Reduzir os outros sons em 50%"),
            new DuckingOption(DuckingPreference.Reduce80, "Reduzir os outros sons em 80%"),
            new DuckingOption(DuckingPreference.MuteOthers, "Silenciar todos os outros sons"),
        ];

        RefreshCommand = new RelayCommand(Refresh);
        CenterBalanceCommand = new RelayCommand(CenterBalance, () => SelectedDevice is not null);
        ApplyProfileCommand = new RelayCommand(ApplyProfile);
        SaveProfileCommand = new RelayCommand(SaveCurrentAsProfile,
            () => SelectedDevice is not null && !string.IsNullOrWhiteSpace(NewProfileName));
        DeleteProfileCommand = new RelayCommand(DeleteProfile);
        RunCheckupCommand = new RelayCommand(() => RunCheckup(notify: false));
        FixIssueCommand = new RelayCommand(FixIssue);
        RestartAudioServiceCommand = new RelayCommand(RestartAudioService);
        RestartElevatedCommand = new RelayCommand(RestartElevated);
        OpenSoundSettingsCommand = new RelayCommand(_system.OpenWindowsSoundSettings);
        OpenLegacyPanelCommand = new RelayCommand(_system.OpenLegacySoundPanel);
        OpenDeviceManagerCommand = new RelayCommand(_system.OpenDeviceManager);

        _catalog.DevicesChanged += OnDevicesChanged;

        LoadSystemSettings();
        LoadProfiles();
        Refresh();

        // Medidores: só rodam com a janela à mostra. Em background o custo cai a zero.
        _meterTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(66),
        };
        _meterTimer.Tick += (_, _) => SelectedDevice?.UpdateMeters();

        // Verificação periódica: só leituras baratas, em intervalo de minutos.
        _checkupTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle);
        _checkupTimer.Tick += (_, _) => RunCheckup(notify: true);
        ConfigureCheckupTimer();

        RunCheckup(notify: false);
    }

    /// <summary>Disparado quando a verificação encontra algo novo digno de notificação.</summary>
    public event EventHandler<HealthIssue>? IssueDetected;

    public ObservableCollection<DeviceViewModel> Devices { get; } = [];

    public ObservableCollection<AudioProfile> Profiles { get; } = [];

    public ObservableCollection<HealthIssue> Issues { get; } = [];

    public IReadOnlyList<DuckingOption> DuckingOptions { get; }

    public ICommand RefreshCommand { get; }

    public ICommand CenterBalanceCommand { get; }

    public ICommand ApplyProfileCommand { get; }

    public ICommand SaveProfileCommand { get; }

    public ICommand DeleteProfileCommand { get; }

    public ICommand RunCheckupCommand { get; }

    public ICommand FixIssueCommand { get; }

    public ICommand RestartAudioServiceCommand { get; }

    public ICommand RestartElevatedCommand { get; }

    public ICommand OpenSoundSettingsCommand { get; }

    public ICommand OpenLegacyPanelCommand { get; }

    public ICommand OpenDeviceManagerCommand { get; }

    public bool IsElevated => _system.IsElevated;

    public bool ShowElevationBanner => !_system.IsElevated;

    public string ElevationLabel => _system.IsElevated ? "ADMIN" : "USUÁRIO";

    public AudioDeviceKind SelectedKind
    {
        get => _selectedKind;
        set
        {
            if (SetProperty(ref _selectedKind, value))
            {
                Refresh();
            }
        }
    }

    public DeviceViewModel? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (_selectedDevice is not null)
            {
                _selectedDevice.OperationCompleted -= OnDeviceOperationCompleted;
            }

            if (!SetProperty(ref _selectedDevice, value))
            {
                return;
            }

            if (value is not null)
            {
                value.OperationCompleted += OnDeviceOperationCompleted;
                value.EnsureDetailsLoaded();
                value.RefreshVolume();
            }

            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsOutputSelected));
            OnPropertyChanged(nameof(IsInputSelected));
        }
    }

    public bool HasSelection => _selectedDevice is not null;

    public bool IsOutputSelected => _selectedDevice?.IsOutput == true;

    public bool IsInputSelected => _selectedDevice?.IsInput == true;

    public DuckingOption? SelectedDucking
    {
        get => _selectedDucking;
        set
        {
            if (!SetProperty(ref _selectedDucking, value) || _suppress || value is null)
            {
                return;
            }

            SetStatus(_system.SetDuckingPreference(value.Value));
        }
    }

    public bool MonoEnabled
    {
        get => _monoEnabled;
        set
        {
            if (!SetProperty(ref _monoEnabled, value) || _suppress)
            {
                return;
            }

            SetStatus(_system.SetMonoEnabled(value));
        }
    }

    public string NewProfileName
    {
        get => _newProfileName;
        set => SetProperty(ref _newProfileName, value);
    }

    /// <summary>Inclui na lista os endpoints ausentes/desativados, úteis para diagnóstico.</summary>
    public bool ShowDisconnected
    {
        get => _showDisconnected;
        set
        {
            if (SetProperty(ref _showDisconnected, value))
            {
                Refresh();
            }
        }
    }

    // ---------------- Preferências do app ----------------

    public bool MinimizeToTray
    {
        get => _settings.Current.MinimizeToTray;
        set
        {
            _settings.Update(s => s with { MinimizeToTray = value });
            OnPropertyChanged();
        }
    }

    public bool StartMinimized
    {
        get => _settings.Current.StartMinimized;
        set
        {
            _settings.Update(s => s with { StartMinimized = value });
            OnPropertyChanged();
        }
    }

    public bool BackgroundCheckupEnabled
    {
        get => _settings.Current.BackgroundCheckupEnabled;
        set
        {
            _settings.Update(s => s with { BackgroundCheckupEnabled = value });
            ConfigureCheckupTimer();
            OnPropertyChanged();
        }
    }

    public bool NotifyOnIssues
    {
        get => _settings.Current.NotifyOnIssues;
        set
        {
            _settings.Update(s => s with { NotifyOnIssues = value });
            OnPropertyChanged();
        }
    }

    public int CheckupIntervalMinutes
    {
        get => _settings.Current.CheckupIntervalMinutes;
        set
        {
            _settings.Update(s => s with { CheckupIntervalMinutes = value });
            ConfigureCheckupTimer();
            OnPropertyChanged();
            OnPropertyChanged(nameof(CheckupIntervalLabel));
        }
    }

    public string CheckupIntervalLabel => $"a cada {_settings.Current.SafeIntervalMinutes} min";

    public bool StartWithWindows
    {
        get => _settings.GetStartWithWindows();
        set
        {
            SetStatus(_settings.SetStartWithWindows(value));
            OnPropertyChanged();
        }
    }

    // ---------------- Verificação ----------------

    public HealthReport LastReport
    {
        get => _lastReport;
        private set
        {
            if (SetProperty(ref _lastReport, value))
            {
                OnPropertyChanged(nameof(HealthSummary));
                OnPropertyChanged(nameof(IsHealthy));
                OnPropertyChanged(nameof(LastCheckupLabel));
            }
        }
    }

    public bool IsHealthy => _lastReport.IsHealthy;

    public string HealthSummary => _lastReport.Summary;

    public string LastCheckupLabel => _lastReport.RunAt == DateTime.MinValue
        ? "ainda não verificado"
        : $"última verificação às {_lastReport.RunAt:HH:mm:ss}";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    public string ProfileStoragePath => _profiles.StoragePath;

    /// <summary>Liga os medidores só quando a janela está visível — economiza CPU em background.</summary>
    public void SetMetersActive(bool active)
    {
        _metersActive = active;

        if (active)
        {
            _meterTimer.Start();
        }
        else
        {
            _meterTimer.Stop();
        }
    }

    public void Refresh()
    {
        var previousId = SelectedDevice?.Id;
        Devices.Clear();

        foreach (var info in _catalog.GetDevices(_selectedKind, _showDisconnected))
        {
            Devices.Add(new DeviceViewModel(info, _volumes, _meters, _quality, _spatial, _drivers));
        }

        SelectedDevice = Devices.FirstOrDefault(d => d.Id == previousId)
                         ?? Devices.FirstOrDefault(d => d.IsDefault)
                         ?? Devices.FirstOrDefault(d => d.IsConnected)
                         ?? Devices.FirstOrDefault();

        var label = _selectedKind == AudioDeviceKind.Input ? "entrada" : "saída";
        StatusMessage = $"{Devices.Count(d => d.IsConnected)} dispositivo(s) de {label} conectado(s).";
        StatusIsError = false;
    }

    public void RunCheckup(bool notify)
    {
        var report = _health.RunCheckup();
        LastReport = report;

        Issues.Clear();
        foreach (var issue in report.Issues.OrderByDescending(i => i.Severity))
        {
            Issues.Add(issue);
        }

        // Só avisa sobre o que ainda não foi avisado, para não virar spam de balão.
        var current = report.Issues.Select(i => i.Signature).ToHashSet();
        _notifiedIssues.IntersectWith(current);

        // Rodando escondido, devolve ao sistema o que a verificação alocou.
        if (!_metersActive)
        {
            MemoryTrimmer.Trim();
        }

        if (!notify || !NotifyOnIssues)
        {
            return;
        }

        foreach (var issue in report.Issues.Where(i => i.Severity != HealthSeverity.Info))
        {
            if (_notifiedIssues.Add(issue.Signature))
            {
                IssueDetected?.Invoke(this, issue);
            }
        }
    }

    public void Dispose()
    {
        _meterTimer.Stop();
        _checkupTimer.Stop();
        _catalog.DevicesChanged -= OnDevicesChanged;
    }

    private void ConfigureCheckupTimer()
    {
        _checkupTimer.Stop();

        if (!_settings.Current.BackgroundCheckupEnabled)
        {
            return;
        }

        _checkupTimer.Interval = TimeSpan.FromMinutes(_settings.Current.SafeIntervalMinutes);
        _checkupTimer.Start();
    }

    private void FixIssue(object? parameter)
    {
        if (parameter is not HealthIssue issue)
        {
            return;
        }

        switch (issue.Kind)
        {
            case HealthIssueKind.ChannelImbalance when issue.DeviceId is not null:
                SetStatus(_volumes.CenterBalance(issue.DeviceId));
                break;

            case HealthIssueKind.DeviceMuted when issue.DeviceId is not null:
                SetStatus(_volumes.SetMuted(issue.DeviceId, false));
                break;

            case HealthIssueKind.MonoEnabled:
                MonoEnabled = false;
                break;

            case HealthIssueKind.DuckingActive:
                SelectedDucking = DuckingOptions.First(o => o.Value == DuckingPreference.DoNothing);
                break;

            case HealthIssueKind.SpatialOnForGaming when issue.DeviceId is not null:
                SetStatus(_spatial.SetFormat(issue.DeviceId, SpatialFormatInfo.Disabled));
                break;

            default:
                StatusMessage = "Esse ponto precisa de ajuste manual.";
                StatusIsError = false;
                return;
        }

        SelectedDevice?.RefreshVolume();
        LoadSystemSettings();
        RunCheckup(notify: false);
    }

    private void CenterBalance()
    {
        if (SelectedDevice is null)
        {
            return;
        }

        SetStatus(_volumes.CenterBalance(SelectedDevice.Id) with
        {
            Message = "Canais igualados — balanço centralizado.",
        });

        SelectedDevice.RefreshVolume();
        RunCheckup(notify: false);
    }

    private void ApplyProfile(object? parameter)
    {
        if (parameter is not AudioProfile profile || SelectedDevice is null)
        {
            return;
        }

        if (SelectedDevice.IsInput)
        {
            StatusMessage = "Perfis se aplicam a dispositivos de saída. Selecione um fone ou caixa.";
            StatusIsError = true;
            return;
        }

        var report = _applier.Apply(profile, SelectedDevice.Id);

        SelectedDevice.RefreshVolume();
        LoadSystemSettings();
        RunCheckup(notify: false);

        if (report.AllSucceeded)
        {
            StatusMessage = report.Summary;
            StatusIsError = false;
            return;
        }

        var details = string.Join("  •  ", report.Failures.Select(f => $"{f.Name}: {f.Result.DisplayMessage}"));
        StatusMessage = report.NeedsElevation
            ? $"{report.Summary} Reinicie como administrador para completar.  •  {details}"
            : $"{report.Summary}  •  {details}";
        StatusIsError = true;
    }

    private void SaveCurrentAsProfile()
    {
        if (SelectedDevice is null || string.IsNullOrWhiteSpace(NewProfileName))
        {
            return;
        }

        var profile = _profiles.CaptureFromDevice(SelectedDevice.Id, NewProfileName.Trim());
        SetStatus(_profiles.Save(profile));
        NewProfileName = string.Empty;
        LoadProfiles();
    }

    private void DeleteProfile(object? parameter)
    {
        if (parameter is not AudioProfile profile)
        {
            return;
        }

        SetStatus(_profiles.Delete(profile.Id));
        LoadProfiles();
    }

    private void RestartAudioService()
    {
        SetStatus(_system.RestartAudioService());
        Refresh();
    }

    private void RestartElevated()
    {
        if (_system.RestartElevated())
        {
            Application.Current.Shutdown();
            return;
        }

        StatusMessage = "Elevação cancelada.";
        StatusIsError = true;
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        foreach (var profile in _profiles.GetProfiles())
        {
            Profiles.Add(profile);
        }
    }

    private void LoadSystemSettings()
    {
        _suppress = true;

        try
        {
            var ducking = _system.GetDuckingPreference();
            _selectedDucking = DuckingOptions.FirstOrDefault(o => o.Value == ducking);
            _monoEnabled = _system.GetMonoEnabled();
        }
        finally
        {
            _suppress = false;
        }

        OnPropertyChanged(nameof(SelectedDucking));
        OnPropertyChanged(nameof(MonoEnabled));
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        // As notificações do Core Audio chegam em uma thread do COM.
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            Refresh();
            RunCheckup(notify: true);
        });
    }

    private void OnDeviceOperationCompleted(object? sender, AudioResult result) => SetStatus(result);

    private void SetStatus(AudioResult result)
    {
        StatusMessage = result.DisplayMessage;
        StatusIsError = !result.Success;
    }
}
