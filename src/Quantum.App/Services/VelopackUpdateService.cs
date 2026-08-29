using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quantum.Audio.Models;
using Velopack;
using Velopack.Sources;

namespace Quantum.App.Services;

/// <summary>O que a checagem de versão encontrou.</summary>
public sealed record UpdateCheckResult(bool HasUpdate, string? Version = null, string? ReleaseNotes = null)
{
    public static UpdateCheckResult UpToDate { get; } = new(false);
}

public interface IUpdateService
{
    /// <summary>
    /// False na versão portátil: sem pasta de instalação, não há para onde
    /// aplicar a atualização. Nesse caso o app avisa e manda para a página.
    /// </summary>
    bool CanSelfUpdate { get; }

    string CurrentVersion { get; }

    Task<UpdateCheckResult> CheckAsync();

    /// <summary>Baixa e aplica a atualização pendente, reiniciando o app.</summary>
    Task<AudioResult> ApplyAsync(IProgress<int>? progress = null);

    void OpenReleasesPage();
}

/// <summary>
/// Verificação e aplicação de atualizações.
/// </summary>
/// <remarks>
/// Dois caminhos, porque o Velopack se recusa a operar fora de uma instalação
/// (<c>NotInstalledException</c>):
///
/// <list type="bullet">
/// <item>Instalado — o Velopack cuida de tudo: compara, baixa em delta, troca a
/// versão e reinicia.</item>
/// <item>Portátil — consulta a API do GitHub direto, só para avisar. Sem pasta de
/// instalação não há o que atualizar, então o botão vira "abrir downloads".</item>
/// </list>
/// </remarks>
public sealed class VelopackUpdateService : IUpdateService, IDisposable
{
    private const string Owner = "net0well";
    private const string Repository = "Quantum";
    private const string RepositoryUrl = $"https://github.com/{Owner}/{Repository}";
    private const string ReleasesUrl = RepositoryUrl + "/releases/latest";
    private const string LatestReleaseApi =
        $"https://api.github.com/repos/{Owner}/{Repository}/releases/latest";

    private readonly ILogger<VelopackUpdateService> _logger;
    private readonly UpdateManager _manager;
    private readonly HttpClient _http;

    private UpdateInfo? _pending;

    public VelopackUpdateService(ILogger<VelopackUpdateService> logger)
    {
        _logger = logger;
        _manager = new UpdateManager(new GithubSource(RepositoryUrl, null, false));

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        // A API do GitHub recusa requisição sem User-Agent.
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Quantum", CurrentVersion));
    }

    public bool CanSelfUpdate => _manager.IsInstalled;

    public string CurrentVersion =>
        _manager.IsInstalled && _manager.CurrentVersion is { } installed
            ? installed.ToString()
            : typeof(VelopackUpdateService).Assembly.GetName().Version?.ToString(3) ?? "desconhecida";

    public async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            return CanSelfUpdate
                ? await CheckInstalledAsync().ConfigureAwait(false)
                : await CheckPortableAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Sem internet, GitHub fora do ar, release malformada: nada disso pode
            // atrapalhar quem só quer mexer no volume.
            _logger.LogWarning(ex, "Falha ao verificar atualizações.");
            return UpdateCheckResult.UpToDate;
        }
    }

    public async Task<AudioResult> ApplyAsync(IProgress<int>? progress = null)
    {
        if (!CanSelfUpdate)
        {
            OpenReleasesPage();
            return AudioResult.Ok(
                "Esta é a versão portátil, que não se atualiza sozinha. Abri a página de downloads.");
        }

        if (_pending is null)
        {
            return AudioResult.Fail("Nenhuma atualização pendente.");
        }

        try
        {
            await _manager.DownloadUpdatesAsync(_pending, p => progress?.Report(p)).ConfigureAwait(false);

            _logger.LogInformation("Atualização baixada; reiniciando na versão nova.");
            _manager.ApplyUpdatesAndRestart(_pending);

            return AudioResult.Ok("Reiniciando na versão nova...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao aplicar a atualização.");
            return AudioResult.Fail(ex.HResult, "Não foi possível aplicar a atualização. Veja o registro.");
        }
    }

    public void OpenReleasesPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = ReleasesUrl, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Não foi possível abrir a página de releases.");
        }
    }

    public void Dispose() => _http.Dispose();

    private async Task<UpdateCheckResult> CheckInstalledAsync()
    {
        _pending = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);

        if (_pending is null)
        {
            _logger.LogInformation("Nenhuma versão nova. Atual: {Version}", CurrentVersion);
            return UpdateCheckResult.UpToDate;
        }

        var release = _pending.TargetFullRelease;
        _logger.LogInformation("Versão {Version} disponível.", release.Version);

        return new UpdateCheckResult(true, release.Version.ToString(), release.NotesMarkdown);
    }

    private async Task<UpdateCheckResult> CheckPortableAsync()
    {
        using var response = await _http.GetAsync(LatestReleaseApi).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("GitHub respondeu {Status} ao procurar a última release.", response.StatusCode);
            return UpdateCheckResult.UpToDate;
        }

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        var tag = document.RootElement.TryGetProperty("tag_name", out var tagElement)
            ? tagElement.GetString()
            : null;

        if (!TryParseVersion(tag, out var latest) ||
            !TryParseVersion(CurrentVersion, out var current) ||
            latest <= current)
        {
            return UpdateCheckResult.UpToDate;
        }

        var notes = document.RootElement.TryGetProperty("body", out var bodyElement)
            ? bodyElement.GetString()
            : null;

        _logger.LogInformation("Versão {Version} disponível (portátil).", latest);
        return new UpdateCheckResult(true, latest.ToString(), notes);
    }

    /// <summary>Aceita "v1.0.8" e "1.0.8", que é o que aparece nas tags e no assembly.</summary>
    internal static bool TryParseVersion(string? raw, out Version version)
    {
        version = new Version(0, 0);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!Version.TryParse(raw.TrimStart('v', 'V').Trim(), out var parsed))
        {
            return false;
        }

        version = parsed;
        return true;
    }
}
