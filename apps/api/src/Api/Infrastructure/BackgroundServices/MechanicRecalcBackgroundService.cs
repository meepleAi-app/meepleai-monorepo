using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Background worker that drains pending <see cref="MechanicRecalcJob"/> rows
/// (ADR-051 M2.1, Sprint 2 / Task 8). Each tick claims the next Pending job via the
/// SKIP-LOCKED repository primitive, enumerates every Published
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysis"/>,
/// dispatches one <see cref="CalculateMechanicAnalysisMetricsCommand"/> per analysis through
/// MediatR, and persists progress on every iteration. A per-job circuit breaker (5 consecutive
/// failures) and an admin-driven cancellation flag short-circuit the loop without poisoning the
/// pool. A stale-heartbeat sweep recovers jobs left in <see cref="RecalcJobStatus.Running"/> by
/// a crashed host.
/// </summary>
/// <remarks>
/// <para>
/// Persistence model: each repository call (<see cref="IMechanicRecalcJobRepository.UpdateAsync"/>
/// + <see cref="IUnitOfWork.SaveChangesAsync"/>) runs in its own DI scope. This avoids the EF
/// change-tracker re-tracking conflict that would arise from repeatedly mapping a fresh entity
/// instance with the same primary key inside one scope (see notes in
/// <see cref="IMechanicRecalcJobRepository.UpdateAsync"/>) and matches the proven pattern used by
/// <see cref="BggImportQueueBackgroundService"/>.
/// </para>
/// <para>
/// Metrics: emits <see cref="MeepleAiMetrics.JobsCompleted"/> with a <c>status</c> tag,
/// <see cref="MeepleAiMetrics.AnalysesProcessed"/>, <see cref="MeepleAiMetrics.AnalysesFailed"/>,
/// <see cref="MeepleAiMetrics.CircuitBreakerOpens"/>, and the
/// <see cref="MeepleAiMetrics.JobDuration"/> histogram (also <c>status</c>-tagged). All counters
/// are static — no DI
/// injection of the metrics façade, mirroring the rest of the observability layer.
/// </para>
/// </remarks>
internal sealed class MechanicRecalcBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MechanicRecalcBackgroundService> _logger;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);
    private const int CircuitBreakerThreshold = 5;
    private static readonly TimeSpan StaleHeartbeatThreshold = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StaleRecoveryInterval = TimeSpan.FromMinutes(5);

    public MechanicRecalcBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MechanicRecalcBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "MechanicRecalcBackgroundService starting. Poll interval: {Poll}s, Circuit breaker: {Threshold} consecutive failures.",
            PollInterval.TotalSeconds,
            CircuitBreakerThreshold);

        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        await RecoverStaleJobsAsync(stoppingToken).ConfigureAwait(false);
        var lastStaleRecovery = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextJobAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: Generic catch prevents service crash; failure logged and loop continues.
            catch (Exception ex)
            {
                _logger.LogError(ex, "MechanicRecalcBackgroundService outer loop");
            }
#pragma warning restore CA1031

            if (DateTime.UtcNow - lastStaleRecovery > StaleRecoveryInterval)
            {
                await RecoverStaleJobsAsync(stoppingToken).ConfigureAwait(false);
                lastStaleRecovery = DateTime.UtcNow;
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("MechanicRecalcBackgroundService stopped");
    }

    /// <summary>
    /// Processes at most one job per call. Returns synchronously when the queue is empty.
    /// </summary>
    private async Task ProcessNextJobAsync(CancellationToken ct)
    {
        // Step 1: claim. The repository transitions Pending → Running atomically and stamps
        // StartedAt; the aggregate returned already reflects that state, so we MUST NOT call
        // MarkRunning again (it would throw on the status guard).
        MechanicRecalcJob? job;
        using (var claimScope = _scopeFactory.CreateScope())
        {
            var repo = claimScope.ServiceProvider.GetRequiredService<IMechanicRecalcJobRepository>();
            job = await repo.ClaimNextPendingAsync(ct).ConfigureAwait(false);
        }

        if (job is null)
        {
            return;
        }

        _logger.LogInformation(
            "Claimed MechanicRecalcJob {JobId} (triggered by {UserId}, started {StartedAt:O})",
            job.Id, job.TriggeredByUserId, job.StartedAt);

        // Step 2: enumerate candidate analyses (id-only projection). Caller iterates by id and
        // each per-id handler reloads the full claim graph itself.
        IReadOnlyList<Guid> ids;
        using (var enumScope = _scopeFactory.CreateScope())
        {
            var analysisRepo = enumScope.ServiceProvider.GetRequiredService<IMechanicAnalysisRepository>();
            ids = await analysisRepo
                .GetIdsByStatusAsync(MechanicAnalysisStatus.Published, ct)
                .ConfigureAwait(false);
        }

        // The aggregate's MarkRunning sets Total and only allows the Pending → Running transition.
        // ClaimNextPendingAsync already moved us to Running, so we patch Total via Reconstitute
        // semantics — i.e. write a fresh entity row with the updated total in a follow-up scope.
        // This matches the documented contract on ClaimNextPendingAsync: "the total counter
        // remains 0; the worker is expected to set it via a follow-up update once the candidate
        // set is enumerated."
        job = await ReconstituteWithTotalAsync(job, ids.Count, ct).ConfigureAwait(false);

        // Step 3: per-id dispatch loop with circuit breaker + cancellation honour.
        foreach (var analysisId in ids)
        {
            if (job.CancellationRequested)
            {
                _logger.LogInformation(
                    "MechanicRecalcJob {JobId} honouring cancellation flag at processed={Processed}/{Total}",
                    job.Id, job.Processed, job.Total);
                job.Complete();
                break;
            }

            if (job.ConsecutiveFailures >= CircuitBreakerThreshold)
            {
                _logger.LogWarning(
                    "MechanicRecalcJob {JobId} circuit breaker tripped at processed={Processed}/{Total}",
                    job.Id, job.Processed, job.Total);
                job.Fail("EmbeddingCircuitBreakerOpen");
                MeepleAiMetrics.CircuitBreakerOpens.Add(1);
                break;
            }

            try
            {
                using var sendScope = _scopeFactory.CreateScope();
                var mediator = sendScope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator
                    .Send(new CalculateMechanicAnalysisMetricsCommand(analysisId), ct)
                    .ConfigureAwait(false);
                job.RecordSuccess(analysisId);
                MeepleAiMetrics.AnalysesProcessed.Add(1);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // Propagate to the outer cancellation handler.
            }
            catch (NotFoundException ex)
            {
                _logger.LogInformation(
                    ex,
                    "MechanicRecalcJob {JobId} skipped analysis {AnalysisId}: row no longer accessible.",
                    job.Id, analysisId);
                job.RecordSkip();
            }
            catch (ConflictException ex)
            {
                _logger.LogInformation(
                    ex,
                    "MechanicRecalcJob {JobId} skipped analysis {AnalysisId}: aggregate state precondition failed.",
                    job.Id, analysisId);
                job.RecordSkip();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // BACKGROUND SERVICE: per-id failure must not poison the pool. Captured on the
            // aggregate (LastError), which feeds the circuit breaker.
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "MechanicRecalcJob {JobId} failed analysis {AnalysisId}",
                    job.Id, analysisId);
                job.RecordFailure(analysisId, ex.Message);
                MeepleAiMetrics.AnalysesFailed.Add(1);
            }
#pragma warning restore CA1031

            if (job.Status == RecalcJobStatus.Running)
            {
                job.Heartbeat();
            }

            await PersistAsync(job, ct).ConfigureAwait(false);
        }

        // Step 4: terminal transition if we exited the loop in Running state (i.e. iteration
        // exhausted candidates without circuit-break or cancellation).
        if (job.Status == RecalcJobStatus.Running)
        {
            job.Complete();
        }

        await PersistAsync(job, ct).ConfigureAwait(false);

        var statusTag = new KeyValuePair<string, object?>("status", job.Status.ToString());
        MeepleAiMetrics.JobsCompleted.Add(1, statusTag);

        // ADR-051 Sprint 2 / Task 11: wall-clock duration histogram from claim → terminal.
        // StartedAt is stamped by ClaimNextPendingAsync; DateTimeOffset.UtcNow matches the
        // aggregate's own timestamp source, so the delta reflects true worker latency.
        if (job.StartedAt is { } startedAt)
        {
            var durationSeconds = (DateTimeOffset.UtcNow - startedAt).TotalSeconds;
            MeepleAiMetrics.JobDuration.Record(durationSeconds, statusTag);
        }

        _logger.LogInformation(
            "MechanicRecalcJob {JobId} finished with status {Status} (processed={Processed}, failed={Failed}, skipped={Skipped})",
            job.Id, job.Status, job.Processed, job.Failed, job.Skipped);
    }

    /// <summary>
    /// Returns a fresh aggregate instance with <c>Total</c> updated and persists the new value.
    /// Necessary because the claim primitive returns a Running job with Total=0 and the aggregate
    /// only allows MarkRunning from Pending. Uses Reconstitute to bypass lifecycle transitions —
    /// this is the same shape the repository uses for its own hydration.
    /// </summary>
    private async Task<MechanicRecalcJob> ReconstituteWithTotalAsync(
        MechanicRecalcJob job,
        int total,
        CancellationToken ct)
    {
        var rebuilt = MechanicRecalcJob.Reconstitute(
            id: job.Id,
            status: job.Status,
            triggeredByUserId: job.TriggeredByUserId,
            total: total,
            processed: job.Processed,
            failed: job.Failed,
            skipped: job.Skipped,
            consecutiveFailures: job.ConsecutiveFailures,
            lastError: job.LastError,
            lastProcessedAnalysisId: job.LastProcessedAnalysisId,
            cancellationRequested: job.CancellationRequested,
            createdAt: job.CreatedAt,
            startedAt: job.StartedAt,
            completedAt: job.CompletedAt,
            heartbeatAt: job.HeartbeatAt);

        await PersistAsync(rebuilt, ct).ConfigureAwait(false);
        return rebuilt;
    }

    /// <summary>
    /// Persists the in-memory aggregate state inside a fresh DI scope. Each call gets its own
    /// scoped DbContext to dodge EF change-tracker re-tracking conflicts that would arise from
    /// repeatedly mapping a new entity with the same key into a single tracked context.
    /// </summary>
    private async Task PersistAsync(MechanicRecalcJob job, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IMechanicRecalcJobRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await repo.UpdateAsync(job, ct).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Recovers jobs left in <see cref="RecalcJobStatus.Running"/> with a heartbeat older than
    /// <see cref="StaleHeartbeatThreshold"/> by failing them with reason <c>StaleHeartbeat</c>.
    /// Runs once on startup and then every <see cref="StaleRecoveryInterval"/> from inside the
    /// main loop. Bypasses the repository (which has no list-stale primitive yet) and queries
    /// the DbContext directly — acceptable because this is a worker-internal recovery sweep.
    /// </summary>
    private async Task RecoverStaleJobsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var repo = scope.ServiceProvider.GetRequiredService<IMechanicRecalcJobRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cutoff = DateTimeOffset.UtcNow - StaleHeartbeatThreshold;

            // Project Ids only so we can hydrate fresh aggregates via the repo's no-tracking
            // path (GetByIdAsync) and avoid colliding change-tracker entries when we later call
            // UpdateAsync on each one in the same scope.
            var staleIds = await dbContext.MechanicRecalcJobs
                .AsNoTracking()
                .Where(j => j.Status == RecalcJobStatus.Running
                    && j.HeartbeatAt != null
                    && j.HeartbeatAt < cutoff)
                .Select(j => j.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (staleIds.Count == 0)
            {
                return;
            }

            foreach (var id in staleIds)
            {
                var job = await repo.GetByIdAsync(id, ct).ConfigureAwait(false);
                if (job is null || job.Status != RecalcJobStatus.Running)
                {
                    continue;
                }

                job.Fail("StaleHeartbeat");
                await repo.UpdateAsync(job, ct).ConfigureAwait(false);
                _logger.LogWarning(
                    "MechanicRecalcJob {JobId} recovered as Failed (StaleHeartbeat — last heartbeat older than {Minutes}min)",
                    id, StaleHeartbeatThreshold.TotalMinutes);
            }

            await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Clean shutdown — caller's outer cancellation handler will surface this.
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // BACKGROUND SERVICE: stale recovery failure must not crash the host.
        catch (Exception ex)
        {
            _logger.LogError(ex, "MechanicRecalcBackgroundService stale-job recovery failed");
        }
#pragma warning restore CA1031
    }
}
