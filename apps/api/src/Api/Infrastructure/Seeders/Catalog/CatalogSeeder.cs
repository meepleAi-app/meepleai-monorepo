using System.Reflection;
using Microsoft.Extensions.Configuration;
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
        .IgnoreUnmatchedProperties()
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
        CancellationToken ct,
        IEmbeddingService? embeddingService = null,
        IConfiguration? configuration = null)
    {
        var manifest = LoadManifest(profile);
        logger.LogInformation("Catalog: {Count} games from {Profile}.yml",
            manifest.Catalog.Games.Count, profile);

        // Step 1: Seed games (SharedGame + GameEntity bridge)
        var gameMap = await GameSeeder.SeedAsync(db, bggService, systemUserId, manifest, logger, ct)
            .ConfigureAwait(false);

        // Step 2: Seed PDFs in Pending state (deferred RAG processing via PdfProcessingQuartzJob)
        await PdfSeeder.SeedAsync(db, manifest, gameMap, systemUserId, logger, ct)
            .ConfigureAwait(false);

        // Step 3: Seed agents for games with seedAgent=true (Dev only)
        if (profile >= SeedProfile.Dev)
        {
            await AgentSeeder.SeedAsync(db, manifest, gameMap, logger, ct)
                .ConfigureAwait(false);
        }
        else
        {
            logger.LogInformation("Catalog: skipping agent seeding (profile: {Profile})", profile);
        }

        // Step 4: Seed strategy patterns for AI agent decision-making
        var seedingEnabled = configuration?.GetValue("Seeding:EnableStrategyPatterns", true) ?? true;
        if (seedingEnabled)
        {
            logger.LogInformation("Seeding strategy patterns for common game openings...");
            await StrategyPatternSeeder.SeedAsync(db, logger, embeddingService, ct)
                .ConfigureAwait(false);
        }

        logger.LogInformation("Catalog seeding complete: {GameCount} games mapped", gameMap.Count);
    }
}
