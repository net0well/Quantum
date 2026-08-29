using System.Windows;
using Wpf.Ui.Appearance;

namespace Quantum.App.Services;

public enum AppTheme
{
    Dark,
    Light,
}

public interface IThemeService
{
    AppTheme Current { get; }

    void Apply(AppTheme theme);
}

/// <summary>
/// Troca a paleta em tempo de execução.
/// </summary>
/// <remarks>
/// As cores moram em <c>Themes/Palette.Dark.xaml</c> e <c>Palette.Light.xaml</c>, com
/// as mesmas chaves; <c>Neon.xaml</c> tem só as regras de estilo e referencia essas
/// chaves por <c>DynamicResource</c>. Trocar o dicionário de paleta na posição certa
/// repinta o app inteiro sem recriar janela nem reiniciar.
/// </remarks>
public sealed class ThemeService : IThemeService
{
    /// <summary>
    /// Posição da paleta em App.xaml. Precisa acompanhar a ordem declarada lá:
    /// 0 = tema do WPF-UI, 1 = controles do WPF-UI, 2 = paleta, 3 = Neon.xaml.
    /// </summary>
    private const int PaletteIndex = 2;

    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public void Apply(AppTheme theme)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var dictionaries = application.Resources.MergedDictionaries;
        if (dictionaries.Count <= PaletteIndex)
        {
            return;
        }

        dictionaries[PaletteIndex] = new ResourceDictionary
        {
            Source = new Uri($"Themes/Palette.{theme}.xaml", UriKind.Relative),
        };

        // A barra de título e os controles do WPF-UI têm paleta própria.
        ApplicationThemeManager.Apply(
            theme == AppTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark);

        Current = theme;
    }
}
