using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Quantum.Audio.Models;
using Quantum.Audio.Storage;

namespace Quantum.Audio.Profiles;

/// <inheritdoc cref="IProfileRepository"/>
public sealed class JsonProfileRepository(IAppPaths paths, ILogger<JsonProfileRepository> logger)
    : IProfileRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public string StoragePath { get; } = paths.ProfilesFile;

    public IReadOnlyList<AudioProfile> Load()
    {
        try
        {
            if (!File.Exists(StoragePath))
            {
                return [];
            }

            var loaded = JsonSerializer.Deserialize<List<AudioProfile>>(
                File.ReadAllText(StoragePath), JsonOptions);

            // Um embutido gravado em disco seria de uma versão antiga: ignora.
            return loaded?.Where(p => !p.IsBuiltIn).ToList() ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Perfis em {Path} ilegíveis; começando vazio.", StoragePath);
            return [];
        }
    }

    public AudioResult Save(IReadOnlyList<AudioProfile> profiles)
    {
        try
        {
            var directory = Path.GetDirectoryName(StoragePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(StoragePath, JsonSerializer.Serialize(profiles, JsonOptions));
            return AudioResult.Ok("Perfil salvo.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Falha ao gravar perfis em {Path}.", StoragePath);
            return AudioResult.Fail(ex.HResult, $"Não foi possível gravar em {StoragePath}.");
        }
    }
}
