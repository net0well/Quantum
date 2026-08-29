using Quantum.App.Services;
using Xunit;

namespace Quantum.Audio.Tests;

/// <summary>
/// As notas vêm do GitHub em Markdown. Estes testes usam trechos reais das
/// releases do Quantum — que são o que o usuário vai ver na faixa de atualização.
/// </summary>
public class ReleaseNotesFormatterTests
{
    [Fact]
    public void Titulo_perde_os_cerquilhas()
    {
        Assert.Equal("Download", ReleaseNotesFormatter.ToPlainText("## Download"));
    }

    [Fact]
    public void Separador_de_tabela_some()
    {
        var result = ReleaseNotesFormatter.ToPlainText("| Arquivo | Para quem |\n|---|---|\n| a | b |");

        Assert.DoesNotContain("---", result);
        Assert.Contains("Arquivo  Para quem", result);
        Assert.Contains("a  b", result);
    }

    [Fact]
    public void Bloco_de_codigo_e_descartado()
    {
        var markdown = "Antes\n```powershell\ndotnet build\n```\nDepois";

        var result = ReleaseNotesFormatter.ToPlainText(markdown);

        Assert.DoesNotContain("dotnet build", result);
        Assert.Contains("Antes", result);
        Assert.Contains("Depois", result);
    }

    [Fact]
    public void Link_vira_so_o_texto()
    {
        var result = ReleaseNotesFormatter.ToPlainText("Veja o [changelog](https://exemplo.com/a.md) hoje");

        Assert.Equal("Veja o changelog hoje", result);
    }

    [Fact]
    public void Item_de_lista_ganha_marcador()
    {
        Assert.Equal("• Corrige o medidor", ReleaseNotesFormatter.ToPlainText("- Corrige o medidor"));
    }

    [Fact]
    public void Crase_e_negrito_somem()
    {
        Assert.Equal("Quantum.exe é o portátil",
            ReleaseNotesFormatter.ToPlainText("`Quantum.exe` é o **portátil**"));
    }

    [Fact]
    public void Citacao_perde_o_sinal_de_maior()
    {
        Assert.Equal("O SmartScreen pode avisar",
            ReleaseNotesFormatter.ToPlainText("> O SmartScreen pode avisar"));
    }

    [Fact]
    public void Linhas_em_branco_seguidas_viram_uma_so()
    {
        var result = ReleaseNotesFormatter.ToPlainText("Um\n\n\n\nDois");

        Assert.Equal($"Um{Environment.NewLine}{Environment.NewLine}Dois", result);
    }

    [Fact]
    public void Notas_longas_sao_cortadas_com_reticencias()
    {
        var markdown = string.Join("\n", Enumerable.Range(1, 40).Select(i => $"Linha {i}"));

        var result = ReleaseNotesFormatter.ToPlainText(markdown, maximumLines: 5);

        Assert.EndsWith("...", result);
        Assert.Contains("Linha 5", result);
        Assert.DoesNotContain("Linha 9", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Notas_vazias_devolvem_vazio(string? markdown)
    {
        Assert.Equal(string.Empty, ReleaseNotesFormatter.ToPlainText(markdown));
    }

    [Fact]
    public void Nota_real_de_release_fica_legivel()
    {
        // Recorte do que o workflow de release gera hoje.
        const string markdown = """
            ## Download

            | Arquivo | Para quem |
            |---|---|
            | `Quantum.exe` | Baixe, execute. Não instala nada. (69,1 MB) |

            Requer Windows 10 (1809+) ou Windows 11, 64 bits.

            > O Windows SmartScreen pode avisar sobre um executável sem assinatura.
            """;

        var result = ReleaseNotesFormatter.ToPlainText(markdown);

        Assert.StartsWith("Download", result);
        Assert.DoesNotContain("|---|", result);
        Assert.DoesNotContain("`", result);
        Assert.DoesNotContain("> ", result);
        Assert.Contains("Requer Windows 10", result);
    }

    [Theory]
    [InlineData("|---|---|", true)]
    [InlineData("| :--- | ---: |", true)]
    [InlineData("| Arquivo | Para quem |", false)]
    [InlineData("texto comum", false)]
    public void Reconhece_separador_de_tabela(string line, bool expected)
    {
        Assert.Equal(expected, ReleaseNotesFormatter.IsTableSeparator(line));
    }
}
