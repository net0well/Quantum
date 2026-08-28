using Quantum.App.Mvvm;
using Quantum.Audio.Models;

namespace Quantum.App.ViewModels;

/// <summary>Um canal do dispositivo, com nível ajustável e pico ao vivo.</summary>
public sealed class ChannelViewModel(ChannelLevel level, Action<int, double> onLevelChanged) : ObservableObject
{
    private double _percent = level.Percent;
    private double _decibels = level.Decibels;
    private double _peak;
    private bool _suppress;

    public int Index { get; } = level.Index;

    public string Label { get; } = level.Label;

    public string ShortLabel { get; } = level.ShortLabel;

    /// <summary>Nível do canal em porcentagem (0–100).</summary>
    public double Percent
    {
        get => _percent;
        set
        {
            if (!SetProperty(ref _percent, value))
            {
                return;
            }

            OnPropertyChanged(nameof(PercentLabel));
            if (!_suppress)
            {
                onLevelChanged(Index, value);
            }
        }
    }

    public double Decibels
    {
        get => _decibels;
        private set
        {
            if (SetProperty(ref _decibels, value))
            {
                OnPropertyChanged(nameof(DecibelsLabel));
            }
        }
    }

    /// <summary>Pico instantâneo de 0 a 1, alimentado pelo medidor.</summary>
    public double Peak
    {
        get => _peak;
        set
        {
            if (SetProperty(ref _peak, value))
            {
                OnPropertyChanged(nameof(PeakPercent));
            }
        }
    }

    public double PeakPercent => _peak * 100.0;

    public string PercentLabel => $"{_percent:N1}%";

    public string DecibelsLabel => $"{_decibels:+0.00;-0.00;0.00} dB";

    /// <summary>Atualiza a partir do sistema sem disparar uma nova gravação.</summary>
    public void Refresh(ChannelLevel level)
    {
        _suppress = true;
        Percent = level.Percent;
        _suppress = false;
        Decibels = level.Decibels;
    }
}
