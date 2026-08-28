using System.ComponentModel;
using System.Windows;
using Quantum.App.ViewModels;
using Wpf.Ui.Controls;

namespace Quantum.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();

        IsVisibleChanged += (_, _) => UpdateMeters();
        StateChanged += (_, _) => UpdateMeters();
    }

    /// <summary>Definido pelo App: quando true, fechar apenas esconde a janela.</summary>
    public bool HideInsteadOfClose { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (HideInsteadOfClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    /// <summary>
    /// Os medidores só fazem sentido com a janela à mostra. Parar o timer quando ela
    /// está escondida ou minimizada é o que mantém o app inerte em segundo plano.
    /// </summary>
    private void UpdateMeters()
    {
        var active = IsVisible && WindowState != WindowState.Minimized;

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SetMetersActive(active);
        }

        if (!active)
        {
            Services.MemoryTrimmer.Trim();
        }
    }
}
