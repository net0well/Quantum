using System.Text;
using System.Text.RegularExpressions;

namespace Quantum.App.Services;

/// <summary>
/// Transforma as notas de release em texto legível.
/// </summary>
/// <remarks>
/// As notas vêm do GitHub em Markdown, com títulos, tabelas, links e blocos de
/// código. Mostrar isso cru numa faixa da interface fica ilegível — aparece
/// <c>## Download</c> e <c>|---|---|</c> na cara do usuário.
///
/// Não é um renderizador de Markdown: é uma limpeza deliberadamente simples, que
/// evita arrastar uma dependência inteira para exibir alguns parágrafos.
/// </remarks>
public static partial class ReleaseNotesFormatter
{
    private const int DefaultMaximumLines = 12;

    public static string ToPlainText(string? markdown, int maximumLines = DefaultMaximumLines)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var lines = new List<string>();
        var insideCodeFence = false;

        foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                insideCodeFence = !insideCodeFence;
                continue;
            }

            if (insideCodeFence || IsTableSeparator(line))
            {
                continue;
            }

            line = CleanLine(line);

            if (line.Length == 0)
            {
                // Uma linha em branco separa parágrafos; várias seguidas, não.
                if (lines.Count > 0 && lines[^1].Length > 0)
                {
                    lines.Add(string.Empty);
                }

                continue;
            }

            lines.Add(line);

            if (lines.Count >= maximumLines)
            {
                lines.Add("...");
                break;
            }
        }

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Linhas como <c>|---|---|</c>, que só existem para desenhar a tabela.</summary>
    internal static bool IsTableSeparator(string line) =>
        line.Length > 0 && line.All(c => c is '|' or '-' or ':' or ' ');

    private static string CleanLine(string line)
    {
        // Título vira só o texto; item de lista ganha marcador de verdade.
        line = HeadingPrefix().Replace(line, string.Empty);
        line = ListPrefix().Replace(line, "• ");

        // Célula de tabela vira texto separado por espaço.
        if (line.StartsWith('|'))
        {
            line = string.Join("  ", line.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(cell => cell.Trim()));
        }

        line = Link().Replace(line, "$1");
        line = line.Replace("`", string.Empty).Replace("**", string.Empty);
        line = BlockQuote().Replace(line, string.Empty);

        return line.Trim();
    }

    [GeneratedRegex(@"^#{1,6}\s*")]
    private static partial Regex HeadingPrefix();

    [GeneratedRegex(@"^[-*+]\s+")]
    private static partial Regex ListPrefix();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"^>\s*")]
    private static partial Regex BlockQuote();
}
