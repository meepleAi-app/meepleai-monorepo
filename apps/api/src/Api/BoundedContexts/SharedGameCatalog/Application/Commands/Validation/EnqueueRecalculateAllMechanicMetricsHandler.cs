using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Handler for <see cref="EnqueueRecalculateAllMechanicMetricsCommand"/>
/// (ADR-051 M2.1, Sprint 2 / Task 9).
/// </summary>
/// <remarks>
/// <para>
/// Creates a <see cref="MechanicRecalcJob"/> in
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Enums.RecalcJobStatus.Pending"/> status
/// via the aggregate's factory, persists it through the repository + UoW pair, and returns the
/// generated job id. The job is now visible to the
/// <see cref="Api.Infrastructure.BackgroundServices.MechanicRecalcBackgroundService"/> worker (Task 8),
/// which will claim it atomically on its next polling tick.
/// </para>
/// <para>
/// The handler does not enumerate or reserve the candidate analyses — that work happens inside the
/// worker once the job transitions to Running, so the caller's request returns immediately with the
/// job id (the endpoint can respond with HTTP 202 Accepted).
/// </para>
/// </remarks>
internal sealed class EnqueueRecalculateAllMechanicMetricsHandler
    : ICommandHandler<EnqueueRecalculateAllMechanicMetricsCommand, Guid>
{
    private readonly IMechanicRecalcJobRepository _jobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EnqueueRecalculateAllMechanicMetricsHandler> _logger;

    public EnqueueRecalculateAllMechanicMetricsHandler(
        IMechanicRecalcJobRepository jobRepository,
        IUnitOfWork unitOfWork,
        ILogger<EnqueueRecalculateAllMechanicMetricsHandler> logger)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(
        EnqueueRecalculateAllMechanicMetricsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var job = MechanicRecalcJob.Enqueue(request.TriggeredByUserId);

        await _jobRepository.AddAsync(job, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Enqueued MechanicRecalcJob {JobId} (triggered by user {UserId}).",
            job.Id,
            request.TriggeredByUserId);

        return job.Id;
    }
}
