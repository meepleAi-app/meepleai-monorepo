using System.Reflection;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Api.Services;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Loads YAML seed manifests and orchestrates catalog-level seeding (games, PDFs, agents).
/// Entry point for the Catalog seeding layer.
/// </summary>
internal static class CatalogSeeder
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Loads and validates the embedded YAML manifest for the given profile.
    /// </summary>
    /// <param name="profile">The seed profile to load (Dev, Staging, Prod).</param>
    /// <returns>A validated <see cref="SeedManifest"/>.</returns>
    /// <exception cref="FileNotFoundException">When profile is None or manifest resource is missing.</exception>
    /// <exception cref="InvalidOperationException">When manifest validation fails.</exception>
    public static SeedManifest LoadManifest(SeedProfile profile)
    {
        if (profile == SeedProfile.None)
            throw new FileNotFoundException($"No manifest for profile '{profile}'");

        var resourceName = $"Api.Infrastructure.Seeders.Catalog.Manifests.{profile.ToString().ToLowerInvariant()}.yml";
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded manifest not found: {resourceName}");
        using var reader = new StreamReader(stream);

        var manifest = YamlDeserializer.Deserialize<SeedManifest>(reader);
        var errors = manifest.Validate(expectedProfile: profile);
        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Manifest validation failed:\n{string.Join("\n", errors)}");

        return manifest;
    }

    /// <summary>
    /// Seeds the catalog layer: games (SharedGame + GameEntity bridge) from the YAML manifest.
    /// PDF and agent seeding will be added in Tasks 6-7.
    /// </summary>
    public static async Task SeedAsync(
        SeedProfile profile,
        MeepleAiDbContext db,
        IBggApiService bggService,
        Guid systemUserId,
        ILogger logger,
        CancellationToken ct)
    {
        var manifest = LoadManifest(profile);
        logger.LogInformation("Catalog: {Count} games from {Profile}.yml",
            manifest.Catalog.Games.Count, profile);

        // Step 1: Seed games (SharedGame + GameEntity bridge)
        var gameMap = await GameSeeder.SeedAsync(db, bggService, systemUserId, manifest, logger, ct)
            .ConfigureAwait(false);

        // Steps 2-4 (PdfSeeder, AgentSeeder, StrategyPatternSeeder) will be added in Tasks 6-7
        logger.LogInformation("Catalog seeding complete: {GameCount} games mapped", gameMap.Count);
    }
}
