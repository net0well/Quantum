using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quantum.Audio.Devices;
using Quantum.Audio.Drivers;
using Quantum.Audio.Health;
using Quantum.Audio.Profiles;
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

        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<IProfileApplier, ProfileApplier>();
        services.AddSingleton<IHealthMonitor, HealthMonitor>();

        return services;
    }
}
