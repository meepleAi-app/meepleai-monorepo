using System.Diagnostics;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.BackgroundTasks;
using Api.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Issue #1823 Wave 3 M9: Quartz singleton scheduler that drives the Wikidata
/// cover enrichment pipeline. Runs every 1 minute via Quartz; per tick:
/// <list type="number">
///   <item>Queries up to <see cref="BatchSize"/> <c>SharedGame</c> IDs ready for
///   enrichment (never-attempted OR retry-eligible per
///   <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.WikidataCoverEnrichmentAttempt.NextRetryAt"/>).</item>
///   <item>Delegates the per-game enrich+record workflow to
///   <see cref="IWikidataCoverEnrichmentRunner"/> — the single source of truth
///   shared with the M12 admin trigger endpoint.</item>
///   <item>Throttles 1s between games to respect the shared Wikimedia 5 RPS cap
///   (DEC-3e) alongside the in-process token bucket.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>Per ADR DEC-3e: single-pod batch (HPA=1) — no distributed lock needed.
/// The <c>[DisallowConcurrentExecution]</c> attribute belt-and-braces enforces
/// the singleton at Quartz level.</para>
///
/// <para>Pattern mirror of <c>CatalogSeedFetchJob</c> (#1903 M5): service-provider
/// scoping per execution, internal <see cref="RunBatchAsync"/> hook for unit
/// tests that drive the batch without spinning up Quartz. Per-item exceptions
/// are caught and logged so a single bad game does not crash the whole batch —
/// the affected attempt simply never gets recorded and the game stays in the
/// "never attempted" set for the next tick.</para>
/// </remarks>
[DisallowConcurrentExecution]
public sealed class WikidataCoverEnrichmentJob : IJob
{
    /// <summary>
    /// Maximum number of games processed per Quartz tick. With the 1s
    /// inter-item throttle that caps throughput at ~30/min/pod, in line with
    /// ADR DEC-3e's 5 RPS budget across the SPARQL + Commons hops per game.
    /// </summary>
    public const int BatchSize = 30;

    /// <summary>Inter-item throttle (ms). Respects the shared 5 RPS Wikimedia cap.</summary>
    public const int DelayBetweenItemsMs = 1000;

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WikidataCoverEnrichmentJob> _logger;

    public WikidataCoverEnrichmentJob(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<WikidataCoverEnrichmentJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// TTL del lease (#3383). Molto sopra la durata attesa di un tick (fino a <see cref="BatchSize"/>
    /// giochi con throttle di <see cref="DelayBetweenItemsMs"/>ms ciascuno): se un'istanza muore a
    /// metà batch, l'enrichment resta fermo al più per questo tempo — preferibile a due istanze che
    /// raddoppiano il rate verso Wikimedia (DEC-3e, violazione ToS).
    /// </summary>
    private static readonly TimeSpan LeaseTtl = TimeSpan.FromMinutes(10);

    private const string LeaseKey = "wikidata-cover-enrichment-batch";

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        _logger.LogDebug("WikidataCoverEnrichmentJob started: FireTime={FireTime}", context.FireTimeUtc);

        using var scope = _serviceProvider.CreateScope();

        var attempts = scope.ServiceProvider.GetRequiredService<IWikidataCoverEnrichmentAttemptRepository>();
        var runner = scope.ServiceProvider.GetRequiredService<IWikidataCoverEnrichmentRunner>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IBackgroundTaskOrchestrator>();

        // #3383 — hard-prevention del vincolo single-pod (ADR-087 D4). [DisallowConcurrentExecution]
        // garantisce l'unicità solo DENTRO un processo: non impedisce a due istanze di eseguire lo
        // stesso batch e raddoppiare il rate verso Wikidata/Commons.
        //
        // FAIL-CLOSED, e non è gratis: se il lease non si acquisisce il tick viene saltato, e se
        // Redis è irraggiungibile l'orchestrator RILANCIA (l'eccezione risale a Quartz come job
        // fallito), quindi l'enrichment SI FERMA. È voluto — non arricchire è preferibile a violare
        // il rate cap Wikimedia — ma significa che un'indisponibilità di Redis ferma anche questa
        // pipeline: vedi il runbook, sezione "Enrichment Wikidata fermo senza errori applicativi".
        var acquired = await orchestrator
            .ExecuteWithDistributedLockAsync(
                LeaseKey,
                innerCt => RunBatchAsync(attempts, runner, innerCt),
                LeaseTtl,
                ct)
            .ConfigureAwait(false);

        if (!acquired)
        {
            _logger.LogInformation(
                "WikidataCoverEnrichmentJob: lease '{LeaseKey}' già detenuto da un'altra istanza — tick saltato. " +
                "Se accade in modo persistente con una sola istanza attiva, il lease è orfano: scadrà entro {TtlMinutes} minuti.",
                LeaseKey, LeaseTtl.TotalMinutes);
        }
    }

    /// <summary>
    /// Internal batch runner — extracted from <see cref="Execute"/> so unit tests
    /// can drive it directly without spinning up the Quartz scheduler.
    /// </summary>
    internal async Task RunBatchAsync(
        IWikidataCoverEnrichmentAttemptRepository attempts,
        IWikidataCoverEnrichmentRunner runner,
        CancellationToken ct)
    {
        // Issue #1823 Wave 3 M11: record tick wall-clock duration regardless of
        // outcome (incl. zero-due ticks) so the histogram reflects real
        // scheduler cadence, not just busy periods.
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await RunBatchInternalAsync(attempts, runner, ct).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            MeepleAiMetrics.WikidataBatchDuration.Record(stopwatch.Elapsed.TotalSeconds);
        }
    }

    private async Task RunBatchInternalAsync(
        IWikidataCoverEnrichmentAttemptRepository attempts,
        IWikidataCoverEnrichmentRunner runner,
        CancellationToken ct)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var dueGameIds = await attempts
            .GetGameIdsDueForEnrichmentAsync(BatchSize, nowUtc, ct)
            .ConfigureAwait(false);

        // Issue #1823 Wave 3 M11: record queue depth for the observable gauge,
        // BEFORE the early-return on empty so ops dashboards see the "no work"
        // signal as 0 rather than the previous tick's stale value.
        MeepleAiMetrics.SetWikidataQueueDepth(dueGameIds.Count);

        if (dueGameIds.Count == 0)
        {
            _logger.LogDebug("WikidataCoverEnrichmentJob: no games due for enrichment this tick.");
            return;
        }

        _logger.LogInformation(
            "WikidataCoverEnrichmentJob: processing {Count} games this tick.",
            dueGameIds.Count);

        var processed = 0;
        for (var i = 0; i < dueGameIds.Count; i++)
        {
            var gameId = dueGameIds[i];

            if (ct.IsCancellationRequested)
            {
                _logger.LogInformation("WikidataCoverEnrichmentJob: cancellation requested after {Processed} games.", processed);
                break;
            }

            try
            {
                // Scheduler always uses forceRefresh=false — freshness window
                // honoured. The admin trigger endpoint uses forceRefresh=true
                // for dogfood re-runs.
                //
                // Issue #1823 Phase F F6: the scheduler tick has no admin actor,
                // so triggeredByAdminUserId stays at its default null — attempt
                // rows authored by the cron are persisted with a null trigger
                // source so the admin UI can distinguish them from manual M12/F2
                // dispatches. Named arg avoids silent positional drift if the
                // runner signature grows in future.
                await runner
                    .EnrichAndRecordAsync(gameId, forceRefresh: false, cancellationToken: ct)
                    .ConfigureAwait(false);
                processed++;
            }
            // OperationCanceledException is intentionally allowed to propagate
            // (filter excludes it); any other exception is logged and the batch
            // continues with the next game.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex,
                    "WikidataCoverEnrichmentJob: unhandled exception while processing game {GameId}; skipping to next.",
                    gameId);
            }

            // Throttle. Sleeps between game N and game N+1, NEVER after the
            // last game (the job's 1-minute trigger interval is already a
            // natural break). The guard uses the loop index, NOT the
            // `processed` counter — otherwise an all-exception batch would
            // skip the rate-limiting delay altogether and slam the next batch
            // back-to-back. Also skipped when the CT is signalled so shutdown
            // drains cleanly via the loop-top check on the next iteration.
            var isLastGame = i == dueGameIds.Count - 1;
            if (!isLastGame
                && DelayBetweenItemsMs > 0
                && !ct.IsCancellationRequested)
            {
                await Task.Delay(DelayBetweenItemsMs, ct).ConfigureAwait(false);
            }
        }

        _logger.LogInformation(
            "WikidataCoverEnrichmentJob: tick complete — processed {Processed}/{Total} games.",
            processed, dueGameIds.Count);
    }
}
