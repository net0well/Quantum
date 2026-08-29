using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quantum.App.Services;
using Quantum.App.ViewModels;
using Quantum.App.Views;
using Quantum.Audio;
using Quantum.Audio.Health;
using Quantum.Audio.Storage;
using Serilog;
using Serilog.Events;

namespace Quantum.App;

/// <summary>
/// Raiz de composição. Usa um <see cref="ServiceCollection"/> puro em vez do Generic Host:
/// o app é uma janela e um ícone de bandeja, e o maquinário de hosted services só
/// acrescentaria peso a um processo que precisa ficar inerte em segundo plano.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _services;
    private TrayIconAdapter? _tray;
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private IAppSettingsService? _settings;
    private ILogger<App>? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // A janela pode ficar escondida na bandeja, então o encerramento é explícito.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var paths = new AppPaths();
        paths.EnsureCreated();
        ConfigureLogging(paths);

        _services = BuildServices(paths);
        _log = _services.GetRequiredService<ILogger<App>>();
        _settings = _services.GetRequiredService<IAppSettingsService>();

        // Antes de qualquer janela existir, para não abrir no tema errado e piscar.
        _services.GetRequiredService<IThemeService>().Apply(_settings.Current.Theme);

        _viewModel = _services.GetRequiredService<MainViewModel>();

        _log.LogInformation("Quantum iniciado. Versão {Version}",
            typeof(App).Assembly.GetName().Version?.ToString() ?? "desconhecida");

        _tray = _services.GetRequiredService<TrayIconAdapter>();
        _tray.OpenRequested += (_, _) => ShowWindow();
        _tray.CheckupRequested += (_, _) => RunCheckupFromTray();
        _tray.ExitRequested += (_, _) => Shutdown();

        _viewModel.IssueDetected += OnIssueDetected;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        var startHidden = _settings.Current.StartMinimized ||
                          e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);

        if (startHidden)
        {
            // A janela nem chega a ser construída: em segundo plano não há árvore
            // visual, nem BAML carregado, nem renderização — só os serviços e a bandeja.
            _log.LogInformation("Iniciando minimizado na bandeja.");
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
        }

        _log?.LogInformation("Quantum encerrado.");

        // O provedor descarta os singletons que implementam IDisposable —
        // serviço de dispositivos, view model e bandeja saem junto.
        _services?.Dispose();
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    private ServiceProvider BuildServices(IAppPaths paths)
    {
        var services = new ServiceCollection();

        services.AddSingleton(paths);
        services.AddQuantumAudio();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: false);
        });

        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<TrayIconAdapter>();
        services.AddSingleton<IDeviceViewModelFactory, DeviceViewModelFactory>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    private static void ConfigureLogging(IAppPaths paths)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(paths.LogsFolder, "quantum-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 5 * 1024 * 1024,
                restrictedToMinimumLevel: LogEventLevel.Information,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug()
            .CreateLogger();
    }

    /// <summary>A janela é criada na primeira vez que alguém pede para vê-la.</summary>
    private void ShowWindow()
    {
        if (_window is null)
        {
            _window = _services!.GetRequiredService<MainWindow>();
            _window.HideInsteadOfClose = _settings?.Current.MinimizeToTray ?? true;
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
        _log?.LogWarning("Problema detectado: {Title} ({Device})", issue.Title, issue.DeviceName);
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

    /// <summary>
    /// Sem isto, uma exceção na thread de interface fecha o app sem deixar rastro.
    /// Com log em arquivo, dá para descobrir o que houve na máquina de outra pessoa.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _log?.LogError(e.Exception, "Exceção não tratada na interface.");
        Log.CloseAndFlush();
    }

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                _log?.LogCritical(exception, "Exceção não tratada fora da interface.");
            }

            Log.CloseAndFlush();
        };
    }
}
