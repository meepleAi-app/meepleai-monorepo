using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.MechanicValidation;

/// <summary>
/// MechanicValidation seed layer (ADR-051 Sprint 1 / Task 40).
/// Seeds a Catan golden set (~40 claims + ~15 BGG tags) so the dashboard,
/// review page, and metrics flow have realistic data on a fresh dev/staging DB.
/// Runs in Staging+Dev profiles, after CatalogSeedLayer (which seeds Catan as a SharedGame).
/// </summary>
internal sealed class MechanicGoldenSeedLayer : ISeedLayer
{
    private const int CatanBggId = 13;
    private const string DataResourceName = "Api.Infrastructure.Seeders.MechanicValidation.Data.catan-golden.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public string Name => "MechanicGolden";

    public SeedProfile MinimumProfile => SeedProfile.Staging;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        var config = context.Services.GetService<IConfiguration>();

        if (config?.GetValue<bool>("SKIP_MECHANIC_GOLDEN_SEED") == true)
        {
            context.Logger.LogInformation(
                "MechanicGoldenSeedLayer: SKIP_MECHANIC_GOLDEN_SEED=true, skipping");
            return;
        }

        // 1. Look up Catan SharedGame by BggId. If missing, log and return (idempotent on partial seeds).
        var catanId = await context.DbContext.SharedGames
            .Where(g => g.BggId == CatanBggId)
            .Select(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (catanId == Guid.Empty)
        {
            context.Logger.LogWarning(
                "MechanicGoldenSeedLayer: Catan SharedGame (BggId={BggId}) not found — skipping. " +
                "Ensure CatalogSeedLayer has run first.",
                CatanBggId);
            return;
        }

        // 2. Idempotency: if any claim already exists for Catan, skip.
        var alreadySeeded = await context.DbContext
            .Set<MechanicGoldenClaimEntity>()
            .AnyAsync(c => c.SharedGameId == catanId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadySeeded)
        {
            context.Logger.LogInformation(
                "MechanicGoldenSeedLayer: Catan golden set already seeded — skipping");
            return;
        }

        // 3. Load curated JSON from embedded resource.
        var data = LoadGoldenData();

        // 4. Dispatch CreateMechanicGoldenClaimCommand for each claim, sequentially.
        var mediator = context.Services.GetRequiredService<IMediator>();

        try
        {
            foreach (var claim in data.Claims)
            {
                var section = ParseSection(claim.Section);

                var command = new CreateMechanicGoldenClaimCommand(
                    SharedGameId: catanId,
                    Section: section,
                    Statement: claim.Statement,
                    ExpectedPage: claim.ExpectedPage,
                    SourceQuote: claim.SourceQuote,
                    CuratorUserId: context.SystemUserId);

                await mediator.Send(command, cancellationToken).ConfigureAwait(false);
            }

            // 5. Bulk-import BGG tags.
            if (data.BggTags.Count > 0)
            {
                var tagDtos = data.BggTags
                    .Select(t => new BggTagDto(t.Name, t.Category))
                    .ToList()
                    .AsReadOnly();

                await mediator.Send(new ImportBggTagsCommand(catanId, tagDtos), cancellationToken)
                    .ConfigureAwait(false);
            }

            context.Logger.LogInformation(
                "MechanicGoldenSeedLayer: seeded {ClaimCount} claims and {TagCount} BGG tags for Catan (SharedGameId={SharedGameId})",
                data.Claims.Count,
                data.BggTags.Count,
                catanId);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                ex,
                "MechanicGoldenSeedLayer: failed to seed Catan golden set (SharedGameId={SharedGameId})",
                catanId);
            throw;
        }
    }

    private static MechanicSection ParseSection(string raw)
    {
        if (!Enum.TryParse<MechanicSection>(raw, ignoreCase: false, out var parsed))
        {
            throw new InvalidOperationException(
                $"MechanicGoldenSeedLayer: unknown MechanicSection '{raw}' in catan-golden.json. " +
                $"Allowed: {string.Join(", ", Enum.GetNames<MechanicSection>())}.");
        }

        return parsed;
    }

    private static GoldenSeedData LoadGoldenData()
    {
        var assembly = typeof(MechanicGoldenSeedLayer).Assembly;
        using var stream = assembly.GetManifestResourceStream(DataResourceName)
            ?? throw new InvalidOperationException(
                $"MechanicGoldenSeedLayer: embedded resource '{DataResourceName}' not found. " +
                "Verify the <EmbeddedResource> include in Api.csproj.");

        var data = JsonSerializer.Deserialize<GoldenSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException(
                $"MechanicGoldenSeedLayer: failed to deserialize '{DataResourceName}'.");

        return data;
    }

    private sealed record GoldenSeedData(
        IReadOnlyList<GoldenClaimDto> Claims,
        IReadOnlyList<GoldenTagDto> BggTags);

    private sealed record GoldenClaimDto(
        string Section,
        string Statement,
        int ExpectedPage,
        string SourceQuote);

    private sealed record GoldenTagDto(string Name, string Category);
}
