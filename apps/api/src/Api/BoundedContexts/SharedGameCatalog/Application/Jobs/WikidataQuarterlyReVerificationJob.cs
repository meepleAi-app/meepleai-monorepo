using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Issue #1823 Wave 3 M15 (ADR DEC-3i) — quarterly QID re-verification cron.
/// Wikidata schema can change (Q-numbers reassigned, P18 deprecated, license
/// retroactively revoked); games enriched once and never re-checked would
/// accumulate stale Cover R2 keys pointing at dead source-attribution links.
///
/// Strategy: every 90 days, reset
/// <see cref="Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity.WikidataQidLastVerifiedAt"/>
/// to <c>NULL</c> for games whose last verification is older than
/// <see cref="ReVerificationWindow"/>. The M9
/// <c>WikidataCoverEnrichmentJob</c> picks them up on its next tick because
/// the freshness-window guard in
/// <c>EnrichCatalogCoverCommandHandler</c> short-circuits only when
/// <c>WikidataQidLastVerifiedAt</c> is non-null AND inside the window —
/// NULL bypasses the guard and triggers a full re-enrichment.
/// </summary>
/// <remarks>
/// <para>Cron schedule: <c>0 0 4 1 */3 ?</c> — 04:00 UTC on the 1st day of
/// every 3rd month (Jan / Apr / Jul / Oct). Late enough that operators have
/// time to monitor without affecting the 03:00 UTC retention sweep.</para>
///
/// <para>Idempotent: re-running the same day touches zero rows on the second
/// pass because the previous pass already reset every eligible row to NULL.</para>
///
/// <para>Internal <see cref="RunOnceAsync"/> hook for unit tests, mirroring
/// the M9 / retention job pattern.</para>
/// </remarks>
[DisallowConcurrentExecution]
public sealed class WikidataQuarterlyReVerificationJob : IJob
{
    /// <summary>DEC-3i re-verification window. 90 days mirrors the M8 freshness short-circuit.</summary>
    public static readonly TimeSpan ReVerificationWindow = TimeSpan.FromDays(90);

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WikidataQuarterlyReVerificationJob> _logger;

    public WikidataQuarterlyReVerificationJob(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<WikidataQuarterlyReVerificationJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        await RunOnceAsync(dbContext, ct).ConfigureAwait(false);
    }

    /// <summary>Internal hook — extracted from <see cref="Execute"/> so unit tests don't need Quartz.</summary>
    internal async Task<int> RunOnceAsync(MeepleAiDbContext dbContext, CancellationToken ct)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = nowUtc - ReVerificationWindow;

        _logger.LogDebug(
            "WikidataQuarterlyReVerificationJob: resetting WikidataQidLastVerifiedAt for games verified before {Cutoff}.",
            cutoff);

        // EF Core ExecuteUpdateAsync translates to a single UPDATE FROM on
        // PostgreSQL — no entity tracking, no SELECT round-trip. The query
        // filter on shared_games (`is_deleted = false`) keeps soft-deleted
        // entries out automatically.
        var reset = await dbContext.SharedGames
            .Where(g => g.WikidataQid != null
                && g.WikidataQidLastVerifiedAt != null
                && g.WikidataQidLastVerifiedAt < cutoff)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(g => g.WikidataQidLastVerifiedAt, (DateTime?)null),
                ct)
            .ConfigureAwait(false);

        if (reset > 0)
        {
            _logger.LogInformation(
                "WikidataQuarterlyReVerificationJob: reset WikidataQidLastVerifiedAt on {Count} game(s) older than {Cutoff} — M9 scheduler will re-enrich on next tick.",
                reset, cutoff);
        }
        else
        {
            _logger.LogDebug(
                "WikidataQuarterlyReVerificationJob: no games older than {Cutoff} — sweep idle.",
                cutoff);
        }

        return reset;
    }
}
