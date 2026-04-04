using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Catalog seed layer: shared games (BGG), PDFs, agents, strategy patterns.
/// Delegates to CatalogSeeder which orchestrates manifest-driven seeding.
/// Runs in Staging and Dev profiles.
/// </summary>
internal sealed class CatalogSeedLayer : ISeedLayer
{
    public string Name => "Catalog";
    public SeedProfile MinimumProfile => SeedProfile.Staging;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        var config = context.Services.GetService<IConfiguration>();
        var bggService = context.Services.GetRequiredService<IBggApiService>();
        var embeddingService = context.Services.GetService<IEmbeddingService>();

        await CatalogSeeder.SeedAsync(
            context.Profile,
            context.DbContext,
            bggService,
            context.SystemUserId,
            context.Logger, cancellationToken,
            embeddingService,
            config).ConfigureAwait(false);
    }
}
