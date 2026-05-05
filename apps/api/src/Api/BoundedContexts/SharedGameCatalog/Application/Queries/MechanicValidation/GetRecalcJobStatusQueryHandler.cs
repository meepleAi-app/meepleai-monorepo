using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Handler for <see cref="GetRecalcJobStatusQuery"/> (ADR-051 M2.1, Sprint 2 / Task 9).
/// </summary>
/// <remarks>
/// <para>
/// Loads the <see cref="MechanicRecalcJob"/> aggregate by id, projects it to a
/// <see cref="RecalcJobStatusDto"/>, and computes a best-effort
/// <see cref="RecalcJobStatusDto.EtaSeconds"/> for jobs in
/// <see cref="RecalcJobStatus.Running"/>. Throws <see cref="NotFoundException"/> for unknown ids
/// so the endpoint returns HTTP 404.
/// </para>
/// <para>
/// Not cached — the aggregate mutates on every worker iteration, so the polling endpoint hits the
/// database every time. See <see cref="GetRecalcJobStatusQuery"/> for rationale.
/// </para>
/// </remarks>
internal sealed class GetRecalcJobStatusQueryHandler
    : IQueryHandler<GetRecalcJobStatusQuery, RecalcJobStatusDto>
{
    private readonly IMechanicRecalcJobRepository _jobRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GetRecalcJobStatusQueryHandler> _logger;

    public GetRecalcJobStatusQueryHandler(
        IMechanicRecalcJobRepository jobRepository,
        TimeProvider timeProvider,
        ILogger<GetRecalcJobStatusQueryHandler> logger)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RecalcJobStatusDto> Handle(
        GetRecalcJobStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var job = await _jobRepository
            .GetByIdAsync(request.JobId, cancellationToken)
            .ConfigureAwait(false);

        if (job is null)
        {
            throw new NotFoundException("MechanicRecalcJob", request.JobId.ToString());
        }

        _logger.LogDebug(
            "Loaded MechanicRecalcJob {JobId} status={Status} processed={Processed}/{Total}",
            job.Id,
            job.Status,
            job.Processed,
            job.Total);

        var etaSeconds = ComputeEtaSeconds(job);

        return new RecalcJobStatusDto(
            Id: job.Id,
            Status: job.Status,
            TriggeredByUserId: job.TriggeredByUserId,
            Total: job.Total,
            Processed: job.Processed,
            Failed: job.Failed,
            Skipped: job.Skipped,
            ConsecutiveFailures: job.ConsecutiveFailures,
            LastError: job.LastError,
            CancellationRequested: job.CancellationRequested,
            CreatedAt: job.CreatedAt,
            StartedAt: job.StartedAt,
            CompletedAt: job.CompletedAt,
            HeartbeatAt: job.HeartbeatAt,
            EtaSeconds: etaSeconds);
    }

    /// <summary>
    /// Best-effort completion estimate for jobs in <see cref="RecalcJobStatus.Running"/>:
    /// <c>(Total - Processed) * (elapsed / Processed)</c>. Returns <c>null</c> for terminal states,
    /// jobs that have not started, jobs with zero processed items, or jobs whose
    /// <see cref="MechanicRecalcJob.Total"/> has not yet been populated by the worker.
    /// </summary>
    /// <remarks>
    /// The "processed" denominator counts only successful items (the aggregate's <c>Processed</c>
    /// counter). Failed and skipped items still consume time and inflate elapsed, which makes the
    /// estimate naturally pessimistic when the worker is hitting failures — that's the desired
    /// signal for the admin UI.
    /// </remarks>
    private double? ComputeEtaSeconds(MechanicRecalcJob job)
    {
        if (job.Status != RecalcJobStatus.Running)
        {
            return null;
        }

        if (job.StartedAt is null)
        {
            return null;
        }

        if (job.Processed <= 0)
        {
            return null;
        }

        if (job.Total <= job.Processed)
        {
            return 0d;
        }

        var now = _timeProvider.GetUtcNow();
        var elapsedSeconds = (now - job.StartedAt.Value).TotalSeconds;
        if (elapsedSeconds <= 0)
        {
            return null;
        }

        var avgPerItem = elapsedSeconds / job.Processed;
        var remaining = job.Total - job.Processed;
        return remaining * avgPerItem;
    }
}
