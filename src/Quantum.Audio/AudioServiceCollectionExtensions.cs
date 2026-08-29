using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quantum.Audio.Devices;
using Quantum.Audio.Drivers;
using Quantum.Audio.Health;
using Quantum.Audio.Health.Strategies;
using Quantum.Audio.Profiles;
using Quantum.Audio.Profiles.Strategies;
using Quantum.Audio.Quality;
using Quantum.Audio.Spatial;
using Quantum.Audio.Storage;
using Quantum.Audio.SystemAudio;

namespace Quantum.Audio;

public static class AudioServiceCollectionExtensions
{
    /// <summary>
    /// Registra a camada de áudio. Tudo é singleton: esses serviços seguram
    /// enumeradores COM vivos, e recriá-los por resolução custaria caro à toa.
    /// </summary>
    public static IServiceCollection AddQuantumAudio(this IServiceCollection services)
    {
        // TryAdd: o app pode registrar caminhos próprios antes de chamar isto.
        services.TryAddSingleton<IAppPaths>(_ => new AppPaths());

        services.AddSingleton<IAudioDeviceCatalog, AudioDeviceCatalog>();
        services.AddSingleton<IAudioVolumeController, AudioVolumeController>();
        services.AddSingleton<IAudioMeterService, AudioMeterService>();
        services.AddSingleton<IAudioQualityService, AudioQualityService>();
        services.AddSingleton<ISpatialAudioService, SpatialAudioService>();
        services.AddSingleton<IDriverService, DriverService>();
        services.AddSingleton<ISystemAudioService, SystemAudioService>();

        services.AddSingleton<IProfileRepository, JsonProfileRepository>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<IProfileApplier, ProfileApplier>();
        services.AddSingleton<IHealthMonitor, HealthMonitor>();

        return services
            .AddProfileStepStrategies()
            .AddHealthCheckStrategies();
    }

    /// <summary>
    /// Cada ajuste que um perfil faz. Acrescentar um novo é registrar mais um aqui —
    /// o <see cref="ProfileApplier"/> não muda.
    /// </summary>
    private static IServiceCollection AddProfileStepStrategies(this IServiceCollection services)
    {
        services.AddSingleton<IProfileStepStrategy, BalanceProfileStepStrategy>();
        services.AddSingleton<IProfileStepStrategy, MasterVolumeProfileStepStrategy>();
        services.AddSingleton<IProfileStepStrategy, SpatialProfileStepStrategy>();
        services.AddSingleton<IProfileStepStrategy, QualityProfileStepStrategy>();
        services.AddSingleton<IProfileStepStrategy, DuckingProfileStepStrategy>();
        services.AddSingleton<IProfileStepStrategy, MonoAudioProfileStepStrategy>();

        return services;
    }

    /// <summary>
    /// Cada verificação da checagem periódica. Mesma ideia: o
    /// <see cref="HealthMonitor"/> não conhece nenhuma delas.
    /// </summary>
    private static IServiceCollection AddHealthCheckStrategies(this IServiceCollection services)
    {
        services.AddSingleton<IHealthCheckStrategy, ChannelImbalanceHealthCheckStrategy>();
        services.AddSingleton<IHealthCheckStrategy, MutedDeviceHealthCheckStrategy>();
        services.AddSingleton<IHealthCheckStrategy, LowVolumeHealthCheckStrategy>();
        services.AddSingleton<IHealthCheckStrategy, SpatialForGamingHealthCheckStrategy>();
        services.AddSingleton<IHealthCheckStrategy, MonoAudioHealthCheckStrategy>();
        services.AddSingleton<IHealthCheckStrategy, DuckingHealthCheckStrategy>();

        return services;
    }
}
