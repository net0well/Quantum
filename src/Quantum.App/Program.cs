using Velopack;

namespace Quantum.App;

/// <summary>
/// Entrada do processo.
/// </summary>
/// <remarks>
/// Existe no lugar do <c>Main</c> gerado pelo WPF por causa de uma exigência do
/// Velopack: instalar, desinstalar e aplicar uma atualização reusam este mesmo
/// executável, passando argumentos próprios. Nesses casos o
/// <see cref="VelopackApp"/> faz o trabalho e encerra o processo aqui — antes de
/// existir janela, contêiner de serviços ou qualquer conexão com o áudio.
///
/// Chamar isso mais tarde (no construtor do App, por exemplo) faria a instalação
/// abrir a interface por um instante antes de sumir.
/// </remarks>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
