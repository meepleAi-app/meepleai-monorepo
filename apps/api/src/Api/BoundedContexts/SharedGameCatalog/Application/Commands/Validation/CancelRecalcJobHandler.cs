using System.Globalization;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Handler for <see cref="CancelRecalcJobCommand"/> (ADR-051 M2.1, Sprint 2 / Task 9).
/// </summary>
/// <remarks>
/// <para>
/// Loads the aggregate, calls
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob.RequestCancellation"/>,
/// persists the flag through the repository + UoW pair. The
/// <see cref="Api.Infrastructure.BackgroundServices.MechanicRecalcBackgroundService"/> worker reads
/// the flag on the next iteration and short-circuits.
/// </para>
/// <para>
/// Converts the aggregate's <see cref="InvalidMechanicRecalcJobTransitionException"/> (raised when
/// the job is in a terminal status) into a <see cref="ConflictException"/> so the endpoint surface
/// returns HTTP 409 consistently with the rest of the validation API. <see cref="NotFoundException"/>
/// is raised for unknown job ids.
/// </para>
/// </remarks>
internal sealed class CancelRecalcJobHandler
    : ICommandHandler<CancelRecalcJobCommand, Unit>
{
    private readonly IMechanicRecalcJobRepository _jobRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;
    private readonly ILogger<CancelRecalcJobHandler> _logger;

    public CancelRecalcJobHandler(
        IMechanicRecalcJobRepository jobRepository,
        IUnitOfWork unitOfWork,
        AuditService auditService,
        ILogger<CancelRecalcJobHandler> logger)
    {
        _jobRepository = jobRepository ?? throw new ArgumentNullException(nameof(jobRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(
        CancelRecalcJobCommand request,
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

        try
        {
            job.RequestCancellation();
        }
        catch (InvalidMechanicRecalcJobTransitionException ex)
        {
            throw new ConflictException(ex.Message);
        }

        await _jobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // ADR-051 Sprint 2 / Task 12: persistent audit trail for who cancelled and when. Captures
        // the job's current Status because cancellation is a flag — the worker performs the actual
        // terminal transition asynchronously.
        await _auditService.LogAsync(
            request.CancelledByUserId.ToString(),
            action: "mechanic_recalc.cancelled",
            resource: "MechanicRecalcJob",
            resourceId: job.Id.ToString(),
            result: "Success",
            details: string.Format(
                CultureInfo.InvariantCulture,
                "Cancellation requested while job in {0} status.",
                job.Status),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Cancellation requested for MechanicRecalcJob {JobId} (current status {Status}).",
            job.Id,
            job.Status);

        return Unit.Value;
    }
}
