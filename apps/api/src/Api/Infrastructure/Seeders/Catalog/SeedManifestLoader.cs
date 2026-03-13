using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Loads and validates YAML seed manifests from data/seed-manifests/.
/// </summary>
internal static class SeedManifestLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Load manifest from a YAML string.
    /// </summary>
    public static SharedGamesManifest LoadFromString(string yaml)
    {
        return Deserializer.Deserialize<SharedGamesManifest>(yaml) ?? new SharedGamesManifest();
    }

    /// <summary>
    /// Load manifest from a file path.
    /// </summary>
    public static SharedGamesManifest LoadFromFile(string filePath, ILogger logger)
    {
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Seed manifest not found: {Path}", filePath);
            return new SharedGamesManifest();
        }

        var yaml = File.ReadAllText(filePath);
        var manifest = LoadFromString(yaml);

        logger.LogInformation("Loaded seed manifest: {Path} ({Count} games)", filePath, manifest.Games.Count);
        return manifest;
    }

    /// <summary>
    /// Validate manifest entries. Returns list of error messages (empty = valid).
    /// </summary>
    public static IReadOnlyList<string> Validate(SharedGamesManifest manifest)
    {
        var errors = new List<string>();

        for (var i = 0; i < manifest.Games.Count; i++)
        {
            var game = manifest.Games[i];
            if (string.IsNullOrWhiteSpace(game.Title))
                errors.Add($"Game at index {i} has empty title");
        }

        var duplicates = manifest.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.Title))
            .GroupBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dup in duplicates)
            errors.Add($"Duplicate game title: '{dup}'");

        return errors;
    }
}
