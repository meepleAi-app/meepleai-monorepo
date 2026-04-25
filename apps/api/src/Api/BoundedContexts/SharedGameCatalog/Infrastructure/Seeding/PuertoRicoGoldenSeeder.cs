using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Seeders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Seeding;

/// <summary>
/// Idempotent Puerto Rico golden claims seeder (ADR-051 Sprint 2 / Task 3).
/// Loads the curated 75-claim fixture from <c>data/rulebook/golden/puerto-rico/golden-claims.json</c>
/// (embedded in the API assembly), and dispatches one
/// <see cref="CreateMechanicGoldenClaimCommand"/> per claim via MediatR.
///
/// Mirrors the existing Catan seeder pattern at
/// <c>Api.Infrastructure.Seeders.MechanicValidation.MechanicGoldenSeedLayer</c> so the two
/// share the same orchestrator-driven lifecycle (Staging+Dev profiles, advisory lock,
/// per-game idempotency).
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotency contract:</b> the layer skips entirely if any
/// <see cref="MechanicGoldenClaimEntity"/> already exists for the Puerto Rico
/// <c>SharedGame</c>. A crash mid-loop therefore leaves a partial seed that subsequent
/// runs will not retry. Operators must manually delete partial Puerto Rico golden claims
/// before re-running. The per-claim exception thrown here includes the failing claim's
/// section + index for recovery.
/// </para>
/// <para>
/// <b>Lookup strategy:</b> <see cref="SharedGameEntity"/> has no <c>Slug</c> column;
/// matching is performed via <c>BggId == 3076</c> (the canonical BGG ID seeded by
/// <c>CatalogSeeder</c> via the dev/staging/prod manifests).
/// </para>
/// <para>
/// <b>BGG tags:</b> NOT seeded by this layer. Sprint 2 / Task 4 introduces the BGG
/// tags loader for Puerto Rico. The seeder is named for the claims set so that Task 4
/// can extend it (or live alongside it) without renaming.
/// </para>
/// </remarks>
internal sealed class PuertoRicoGoldenSeeder : ISeedLayer
{
    /// <summary>BGG ID for Puerto Rico — matches CatalogSeeder manifests.</summary>
    private const int PuertoRicoBggId = 3076;

    /// <summary>
    /// Logical name of the embedded JSON resource. Matches the
    /// <c>LogicalName</c> attribute on the <c>EmbeddedResource</c> entry in
    /// <c>Api.csproj</c>.
    /// </summary>
    private const string EmbeddedResourceName =
        "Api.BoundedContexts.SharedGameCatalog.Infrastructure.Seeding.Data.puerto-rico-golden-claims.json";

    /// <summary>
    /// Configuration / env-var key that disables this seeder.
    /// </summary>
    private const string SkipFlagKey = "SKIP_PUERTO_RICO_GOLDEN_SEED";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public string Name => "PuertoRicoGolden";

    public SeedProfile MinimumProfile => SeedProfile.Staging;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var config = context.Services.GetService<IConfiguration>();
        if (config?.GetValue<bool>(SkipFlagKey) == true)
        {
            context.Logger.LogInformation(
                "PuertoRicoGoldenSeeder: {Flag}=true, skipping",
                SkipFlagKey);
            return;
        }

        // 1. Resolve Puerto Rico SharedGame by BggId.
        var prSharedGameId = await context.DbContext.SharedGames
            .Where(g => g.BggId == PuertoRicoBggId)
            .Select(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (prSharedGameId == Guid.Empty)
        {
            context.Logger.LogWarning(
                "PuertoRicoGoldenSeeder: Puerto Rico SharedGame (BggId={BggId}) not found — skipping. " +
                "Ensure CatalogSeedLayer has run first.",
                PuertoRicoBggId);
            return;
        }

        // 2. Idempotency: skip if any claim already exists for Puerto Rico.
        var alreadySeeded = await context.DbContext
            .Set<MechanicGoldenClaimEntity>()
            .AnyAsync(c => c.SharedGameId == prSharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadySeeded)
        {
            context.Logger.LogInformation(
                "PuertoRicoGoldenSeeder: Puerto Rico golden set already seeded — skipping");
            return;
        }

        // 3. Load curated JSON from embedded resource.
        var claims = LoadEmbeddedClaims();
        if (claims.Count == 0)
        {
            throw new InvalidOperationException(
                "Puerto Rico golden seed JSON contained no claims; refusing to seed an empty golden set.");
        }

        // 4. Dispatch CreateMechanicGoldenClaimCommand for each claim, sequentially.
        var mediator = context.Services.GetRequiredService<IMediator>();

        try
        {
            for (var i = 0; i < claims.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var claim = claims[i];

                try
                {
                    var section = ParseSection(claim.Section);

                    var command = new CreateMechanicGoldenClaimCommand(
                        SharedGameId: prSharedGameId,
                        Section: section,
                        Statement: claim.Statement,
                        ExpectedPage: claim.ExpectedPage,
                        SourceQuote: claim.SourceQuote,
                        CuratorUserId: context.SystemUserId);

                    await mediator.Send(command, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    throw new InvalidOperationException(
                        $"Failed to seed Puerto Rico golden claim at index {i} " +
                        $"(section={claim.Section}, page={claim.ExpectedPage}). " +
                        "Partial seed may exist; manual cleanup required before retry.",
                        ex);
                }
            }

            context.Logger.LogInformation(
                "PuertoRicoGoldenSeeder: seeded {ClaimCount} claims for Puerto Rico (SharedGameId={SharedGameId})",
                claims.Count,
                prSharedGameId);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(
                ex,
                "PuertoRicoGoldenSeeder: failed to seed Puerto Rico golden set (SharedGameId={SharedGameId})",
                prSharedGameId);
            throw;
        }
    }

    /// <summary>
    /// Test hook — exposes the embedded JSON loader so unit tests can verify
    /// the resource was wired and parses correctly without spinning up a DB.
    /// </summary>
    internal static IReadOnlyList<GoldenClaimDto> LoadEmbeddedClaimsForTest() => LoadEmbeddedClaims();

    /// <summary>
    /// Validate that <paramref name="section"/> is a defined member of the
    /// <see cref="MechanicSection"/> enum (0..5). The Puerto Rico fixture stores
    /// section as integer (unlike Catan which uses string names).
    /// </summary>
    private static MechanicSection ParseSection(int section)
    {
        if (!Enum.IsDefined(typeof(MechanicSection), section))
        {
            throw new InvalidOperationException(
                $"PuertoRicoGoldenSeeder: unknown MechanicSection value {section} in puerto-rico-golden-claims.json. " +
                $"Allowed: 0..{Enum.GetValues<MechanicSection>().Length - 1}.");
        }

        return (MechanicSection)section;
    }

    private static IReadOnlyList<GoldenClaimDto> LoadEmbeddedClaims()
    {
        var assembly = typeof(PuertoRicoGoldenSeeder).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"PuertoRicoGoldenSeeder: embedded resource '{EmbeddedResourceName}' not found. " +
                "Verify the <EmbeddedResource> include in Api.csproj points at " +
                "data/rulebook/golden/puerto-rico/golden-claims.json with the correct LogicalName.");

        var data = JsonSerializer.Deserialize<GoldenSeedData>(stream, JsonOptions)
            ?? throw new InvalidOperationException(
                $"PuertoRicoGoldenSeeder: failed to deserialize '{EmbeddedResourceName}'.");

        return data.Claims ?? Array.Empty<GoldenClaimDto>();
    }

    /// <summary>
    /// Top-level shape of the Puerto Rico fixture. Only <c>claims</c> is consumed by
    /// this seeder; <c>sharedGameSlug</c>, <c>edition</c>, and <c>rulebookPdfSha256</c>
    /// are documented in the fixture as metadata for human curators and Task 2's
    /// coverage audit. <c>bggTags</c> lives in a separate sibling file and is wired
    /// in Sprint 2 / Task 4.
    /// </summary>
    private sealed record GoldenSeedData(IReadOnlyList<GoldenClaimDto>? Claims);

    /// <summary>
    /// Per-claim DTO. Matches the camelCase JSON keys used in the fixture.
    /// <c>Keywords</c> is preserved for completeness/diagnostics but is not forwarded
    /// to the command — <see cref="Domain.Aggregates.MechanicGoldenClaim.CreateAsync"/>
    /// extracts keywords from the statement via <c>IKeywordExtractor</c>.
    /// </summary>
    internal sealed record GoldenClaimDto(
        int Section,
        string Statement,
        int ExpectedPage,
        string SourceQuote,
        IReadOnlyList<string>? Keywords);
}
