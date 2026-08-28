using Quantum.Audio.Devices;
using Quantum.Audio.Drivers;
using Quantum.Audio.Models;
using Quantum.Audio.Quality;
using Quantum.Audio.Spatial;

namespace Quantum.App.ViewModels;

/// <summary>
/// Cria os view models de dispositivo.
/// </summary>
/// <remarks>
/// Padrão Factory. Sem ele, quem monta a lista precisa carregar cinco serviços
/// que não usa para nada além de repassar ao construtor — e todo serviço novo que
/// o <see cref="DeviceViewModel"/> passar a precisar vazaria para lá também.
/// </remarks>
public interface IDeviceViewModelFactory
{
    DeviceViewModel Create(AudioDeviceInfo info);
}

/// <inheritdoc cref="IDeviceViewModelFactory"/>
public sealed class DeviceViewModelFactory(
    IAudioVolumeController volumes,
    IAudioMeterService meters,
    IAudioQualityService quality,
    ISpatialAudioService spatial,
    IDriverService drivers) : IDeviceViewModelFactory
{
    public DeviceViewModel Create(AudioDeviceInfo info) =>
        new(info, volumes, meters, quality, spatial, drivers);
}
