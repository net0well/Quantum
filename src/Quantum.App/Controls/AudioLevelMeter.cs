using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Quantum.Audio.Metering;

namespace Quantum.App.Controls;

/// <summary>
/// Medidor de pico com escala em dB, balística e traço de pico.
/// </summary>
/// <remarks>
/// É um <see cref="FrameworkElement"/> que desenha em <see cref="OnRender"/>, e não
/// uma pilha de <c>Border</c>: com vinte medidores na tela, a contagem de elementos
/// visuais é o que pesa no WPF, não o desenho em si.
///
/// A alimentação vem em <see cref="Amplitude"/>, atualizada pelo view model a 30 Hz.
/// O desenho acompanha o relógio de composição a 60 fps e a balística interpola
/// entre as amostras — por isso dá para cortar as chamadas COM pela metade sem que
/// o movimento fique travado.
/// </remarks>
public sealed class AudioLevelMeter : FrameworkElement
{
    public static readonly DependencyProperty AmplitudeProperty = DependencyProperty.Register(
        nameof(Amplitude), typeof(double), typeof(AudioLevelMeter),
        new PropertyMetadata(0.0));

    public static readonly DependencyProperty ShowScaleProperty = DependencyProperty.Register(
        nameof(ShowScale), typeof(bool), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowBarProperty = DependencyProperty.Register(
        nameof(ShowBar), typeof(bool), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(
        nameof(TrackBrush), typeof(Brush), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(Brushes.DimGray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty NormalBrushProperty = DependencyProperty.Register(
        nameof(NormalBrush), typeof(Brush), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(Brushes.MediumSpringGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WarningBrushProperty = DependencyProperty.Register(
        nameof(WarningBrush), typeof(Brush), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(Brushes.Goldenrod, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DangerBrushProperty = DependencyProperty.Register(
        nameof(DangerBrush), typeof(Brush), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(Brushes.OrangeRed, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PeakBrushProperty = DependencyProperty.Register(
        nameof(PeakBrush), typeof(Brush), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ScaleBrushProperty = DependencyProperty.Register(
        nameof(ScaleBrush), typeof(Brush), typeof(AudioLevelMeter),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double SegmentWidth = 4.0;
    private const double SegmentGap = 2.0;
    private const double ScaleHeight = 13.0;

    private readonly MeterBallistics _ballistics = new();

    private TimeSpan _lastRenderTime = TimeSpan.MinValue;
    private double _lastDrawnLevel = -1;
    private double _lastDrawnPeak = -1;
    private bool _lastDrawnClipping;
    private bool _subscribed;

    public AudioLevelMeter()
    {
        Loaded += (_, _) => Subscribe();
        Unloaded += (_, _) => Unsubscribe();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                Subscribe();
            }
            else
            {
                Unsubscribe();
            }
        };
    }

    /// <summary>Pico bruto de 0 a 1, como vem da API de áudio.</summary>
    public double Amplitude
    {
        get => (double)GetValue(AmplitudeProperty);
        set => SetValue(AmplitudeProperty, value);
    }

    /// <summary>Desenha as marcações em dB abaixo da barra.</summary>
    public bool ShowScale
    {
        get => (bool)GetValue(ShowScaleProperty);
        set => SetValue(ShowScaleProperty, value);
    }

    /// <summary>
    /// Com <c>false</c>, desenha só a régua. Serve para pôr uma referência única
    /// embaixo de um grupo de medidores em vez de repeti-la em cada canal.
    /// </summary>
    public bool ShowBar
    {
        get => (bool)GetValue(ShowBarProperty);
        set => SetValue(ShowBarProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public Brush NormalBrush
    {
        get => (Brush)GetValue(NormalBrushProperty);
        set => SetValue(NormalBrushProperty, value);
    }

    public Brush WarningBrush
    {
        get => (Brush)GetValue(WarningBrushProperty);
        set => SetValue(WarningBrushProperty, value);
    }

    public Brush DangerBrush
    {
        get => (Brush)GetValue(DangerBrushProperty);
        set => SetValue(DangerBrushProperty, value);
    }

    public Brush PeakBrush
    {
        get => (Brush)GetValue(PeakBrushProperty);
        set => SetValue(PeakBrushProperty, value);
    }

    public Brush ScaleBrush
    {
        get => (Brush)GetValue(ScaleBrushProperty);
        set => SetValue(ScaleBrushProperty, value);
    }

    /// <summary>Apaga o indicador de clipping, depois que o usuário o viu.</summary>
    public void ResetClipping()
    {
        _ballistics.ResetClipping();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var barHeight = !ShowBar ? 0
            : ShowScale ? Math.Max(height - ScaleHeight, 4)
            : height;

        if (ShowBar)
        {
            DrawSegments(drawingContext, width, barHeight, _ballistics.Level);
            DrawPeakMarker(drawingContext, width, barHeight, _ballistics.PeakHold);

            if (_ballistics.IsClipping)
            {
                // Bloco no fim da barra, que fica aceso mesmo depois do som passar.
                drawingContext.DrawRectangle(DangerBrush, null,
                    new Rect(width - SegmentWidth, 0, SegmentWidth, barHeight));
            }
        }

        if (ShowScale)
        {
            DrawScale(drawingContext, width, barHeight);
        }
    }

    private void DrawSegments(DrawingContext context, double width, double barHeight, double level)
    {
        var step = SegmentWidth + SegmentGap;
        var warning = AudioLevelScale.ToPosition(AudioLevelScale.WarningDecibels);
        var danger = AudioLevelScale.ToPosition(AudioLevelScale.DangerDecibels);

        for (var x = 0.0; x + SegmentWidth <= width; x += step)
        {
            var position = (x + (SegmentWidth / 2)) / width;
            var lit = position <= level;

            var brush = !lit
                ? TrackBrush
                : position >= danger ? DangerBrush
                : position >= warning ? WarningBrush
                : NormalBrush;

            context.DrawRectangle(brush, null, new Rect(x, 0, SegmentWidth, barHeight));
        }
    }

    private void DrawPeakMarker(DrawingContext context, double width, double barHeight, double peak)
    {
        if (peak <= 0.001)
        {
            return;
        }

        var x = Math.Clamp(peak * width, 0, width - 2);
        context.DrawRectangle(PeakBrush, null, new Rect(x, 0, 2, barHeight));
    }

    private void DrawScale(DrawingContext context, double width, double barHeight)
    {
        var pen = new Pen(ScaleBrush, 1);
        pen.Freeze();

        var typeface = new Typeface("Cascadia Mono, Consolas, Courier New");
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        foreach (var decibels in AudioLevelScale.Ticks)
        {
            var x = Math.Clamp(AudioLevelScale.ToPosition(decibels) * width, 0, width - 1);
            context.DrawLine(pen, new Point(x, barHeight + 1), new Point(x, barHeight + 4));

            var label = decibels == 0 ? "0" : decibels.ToString("0", CultureInfo.InvariantCulture);
            var text = new FormattedText(
                label,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                8.0,
                ScaleBrush,
                pixelsPerDip);

            // Encosta o rótulo do 0 dB na borda para não sair da área do controle.
            var textX = Math.Clamp(x - (text.Width / 2), 0, width - text.Width);
            context.DrawText(text, new Point(textX, barHeight + 4));
        }
    }

    private void Subscribe()
    {
        // Régua sozinha não anima; não faz sentido acordar a cada quadro por ela.
        if (_subscribed || !ShowBar)
        {
            return;
        }

        _subscribed = true;
        _lastRenderTime = TimeSpan.MinValue;
        CompositionTarget.Rendering += OnRendering;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _subscribed = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args)
        {
            return;
        }

        // O evento chega mais de uma vez por quadro; só o primeiro conta.
        if (args.RenderingTime == _lastRenderTime)
        {
            return;
        }

        var elapsed = _lastRenderTime == TimeSpan.MinValue
            ? TimeSpan.FromMilliseconds(16)
            : args.RenderingTime - _lastRenderTime;

        _lastRenderTime = args.RenderingTime;
        _ballistics.Push(Amplitude, elapsed);

        // Redesenhar sem mudança visível é desperdício com muitos medidores na tela.
        if (Math.Abs(_ballistics.Level - _lastDrawnLevel) < 0.002 &&
            Math.Abs(_ballistics.PeakHold - _lastDrawnPeak) < 0.002 &&
            _ballistics.IsClipping == _lastDrawnClipping)
        {
            return;
        }

        _lastDrawnLevel = _ballistics.Level;
        _lastDrawnPeak = _ballistics.PeakHold;
        _lastDrawnClipping = _ballistics.IsClipping;
        InvalidateVisual();
    }
}
