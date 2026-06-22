using Api.BoundedContexts.Administration.Application.Commands;
using Api.Infrastructure.Seeders.Catalog.SeedBlob;
using Api.Services;
using Api.Services.Pdf;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Api.Infrastructure.Seeders;

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

        if (config?.GetValue<bool>("SKIP_CATALOG_SEED") == true)
        {
            context.Logger.LogInformation("CatalogSeedLayer: SKIP_CATALOG_SEED=true, skipping");
            return;
        }

        var bggService = context.Services.GetRequiredService<IBggApiService>();
        var embeddingService = context.Services.GetService<IEmbeddingService>();
        var primaryBlob = context.Services.GetRequiredService<IBlobStorageService>();
        var seedBlob = context.Services.GetRequiredService<ISeedBlobReader>();

        // Normalize blank/whitespace to null so an env var set to "" in docker-compose
        // or .env doesn't propagate as an empty manifest name into LoadManifest
        // (which would build "Manifests..yml" and throw FileNotFoundException).
        var manifestOverride = config?.GetValue<string>("SEED_CATALOG_MANIFEST_OVERRIDE");
        if (string.IsNullOrWhiteSpace(manifestOverride))
        {
            manifestOverride = null;
        }
        else
        {
            context.Logger.LogInformation(
                "CatalogSeedLayer: using manifest override '{Manifest}'", manifestOverride);
        }

        await CatalogSeeder.SeedAsync(
            context.Profile,
            context.DbContext,
            bggService,
            context.SystemUserId,
            primaryBlob,
            seedBlob,
            context.Logger, cancellationToken,
            embeddingService,
            config,
            manifestNameOverride: manifestOverride).ConfigureAwait(false);

        // Badsworm dogfood persona: 10 library entries (incl. Nanolith) + mock KB
        // documents for dashboard validation. Runs after CatalogSeeder so the
        // SharedGames it references are guaranteed to exist. Idempotent.
        try
        {
            var mediator = context.Services.GetRequiredService<IMediator>();
            await mediator.Send(new SeedBadswormPersonaCommand(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "[Catalog] Badsworm persona seed failed — continuing");
        }

        // Issue #1389: seed Nanolith multi-book gamebook aggregates after the
        // CatalogSeeder + PdfSeeder pipeline runs (SharedGames and PdfDocuments must
        // already exist). Best-effort: failure must not block the seed pipeline.
        // Idempotent: skipped on subsequent runs if Nanolith GameBooks are present.
        // Scope limited to Nanolith (already in catalog manifest); Fighting Fantasy and
        // Maracaibo require a catalog manifest update and remain follow-up.
        try
        {
            var gameBookSeeder = context.Services.GetRequiredService<GameBookSeeder>();
            await gameBookSeeder
                .SeedNanolithIfReadyAsync(context.DbContext, context.SystemUserId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "[Catalog] GameBookSeeder.SeedNanolithIfReadyAsync failed — continuing");
        }
    }
}
