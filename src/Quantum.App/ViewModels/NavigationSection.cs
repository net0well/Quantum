using Quantum.Audio.Models;

namespace Quantum.App.ViewModels;

/// <summary>Um item da barra lateral.</summary>
/// <param name="Key">Identificador estável usado para decidir o que mostrar.</param>
/// <param name="Label">Texto exibido.</param>
/// <param name="Icon">Chave da geometria em Themes/Neon.xaml.</param>
/// <param name="Kind">
/// Quando preenchido, selecionar a seção também troca a direção dos dispositivos
/// listados — é o que substitui o antigo alternador Saída/Entrada.
/// </param>
public sealed record NavigationSection(
    string Key,
    string Label,
    string Icon,
    AudioDeviceKind? Kind = null)
{
    public const string Dashboard = "painel";
    public const string Output = "saida";
    public const string Input = "entrada";
    public const string Settings = "ajustes";

    /// <summary>Seções que mostram a lista de dispositivos e os controles por dispositivo.</summary>
    public bool IsDeviceSection => Kind is not null;
}
