using System.ComponentModel;
using System.Windows;
using Quantum.App.Services;
using Quantum.App.ViewModels;
using Quantum.App.Views;
using Quantum.Audio.Devices;
using Quantum.Audio.Drivers;
using Quantum.Audio.Health;
using Quantum.Audio.Profiles;
using Quantum.Audio.Quality;
using Quantum.Audio.Spatial;
using Quantum.Audio.SystemAudio;

namespace Quantum.App;

/// <summary>
/// Raiz de composição. O grafo é pequeno e fixo, então a montagem é explícita —
/// um contêiner de DI aqui só acrescentaria indireção.
/// </summary>
public partial class App : Application
{
    private AudioDeviceService? _deviceService;
    private MainViewModel? _viewModel;
    private TrayIconService? _tray;
    private MainWindow? _window;
    private AppSettingsService? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A janela pode ficar escondida na bandeja, então o encerramento é explícito.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settings = new AppSettingsService();

        _deviceService = new AudioDeviceService();
        var quality = new AudioQualityService();
        var spatial = new SpatialAudioService();
        var drivers = new DriverService();
        var system = new SystemAudioService();
        var profiles = new ProfileService(_deviceService, spatial, system);
        var applier = new ProfileApplier(_deviceService, quality, spatial, system);
        var health = new HealthMonitor(_deviceService, spatial, system);

        _viewModel = new MainViewModel(
            _deviceService, quality, spatial, drivers, system, profiles, applier, health, settings);

        _tray = new TrayIconService();
        _tray.OpenRequested += (_, _) => ShowWindow();
        _tray.CheckupRequested += (_, _) => RunCheckupFromTray();
        _tray.ExitRequested += (_, _) => Shutdown();

        _viewModel.IssueDetected += OnIssueDetected;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _settings = settings;

        var startHidden = settings.Current.StartMinimized ||
                          e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);

        if (startHidden)
        {
            // A janela nem chega a ser construída: em segundo plano não há árvore
            // visual, nem BAML carregado, nem renderização — só o serviço e a bandeja.
            _tray.SetTooltip("Quantum — rodando em segundo plano");
            MemoryTrimmer.Trim();
        }
        else
        {
            ShowWindow();
        }

        UpdateTrayTooltip();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.IssueDetected -= OnIssueDetected;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.Dispose();
        }

        _tray?.Dispose();
        _deviceService?.Dispose();
        base.OnExit(e);
    }

    /// <summary>A janela é criada na primeira vez que alguém pede para vê-la.</summary>
    private void ShowWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow
            {
                DataContext = _viewModel,
                HideInsteadOfClose = _settings?.Current.MinimizeToTray ?? true,
            };

            MainWindow = _window;
        }

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void RunCheckupFromTray()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.RunCheckup(notify: false);
        UpdateTrayTooltip();
        _tray?.ShowBalloon("Quantum", _viewModel.HealthSummary, !_viewModel.IsHealthy);
    }

    private void OnIssueDetected(object? sender, HealthIssue issue)
    {
        _tray?.ShowBalloon($"Quantum — {issue.Title}", issue.Detail, true);
        UpdateTrayTooltip();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.MinimizeToTray) when _window is not null && _viewModel is not null:
                _window.HideInsteadOfClose = _viewModel.MinimizeToTray;
                break;

            case nameof(MainViewModel.LastReport):
                UpdateTrayTooltip();
                break;
        }
    }

    private void UpdateTrayTooltip()
    {
        if (_viewModel is null)
        {
            return;
        }

        _tray?.SetTooltip(_viewModel.IsHealthy
            ? "Quantum — áudio ok"
            : $"Quantum — {_viewModel.LastReport.Issues.Count} ponto(s) de atenção");
    }
}
