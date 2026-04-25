using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Handler for <see cref="OverrideCertificationCommand"/> (ADR-051 Sprint 1 / Task 24).
/// Applies the admin-override certification path: mutates the aggregate via
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicAnalysis.CertifyViaOverride"/>,
/// raises <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Events.MechanicAnalysisCertifiedEvent"/>
/// with <c>WasOverride=true</c>, and commits through a single unit-of-work boundary.
/// </summary>
/// <remarks>
/// <para>
/// Mirror of the Task 23 "mutate-then-explicit-raise" pattern used for the automatic path. The
/// aggregate's <c>CertifyViaOverride</c> intentionally does NOT raise the event itself: the caller
/// owns the UoW boundary and decides when the state change is about to be committed.
/// </para>
/// <para>
/// Pre-checks convert the aggregate's defensive <see cref="InvalidOperationException"/> branches
/// into <see cref="ConflictException"/> (HTTP 409) so the endpoint surface stays consistent with
/// Task 23 conventions. The aggregate's guards remain in place for defense-in-depth.
/// </para>
/// </remarks>
internal sealed class OverrideCertificationHandler
    : ICommandHandler<OverrideCertificationCommand, Unit>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OverrideCertificationHandler> _logger;

    public OverrideCertificationHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        ILogger<OverrideCertificationHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(
        OverrideCertificationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysis = await _analysisRepository
            .GetByIdAsync(request.MechanicAnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            throw new NotFoundException("MechanicAnalysis", request.MechanicAnalysisId.ToString());
        }

        if (analysis.LastMetricsId is null)
        {
            throw new ConflictException(
                $"MechanicAnalysis {analysis.Id} has no prior metrics; cannot override certification.");
        }

        if (analysis.CertificationStatus == CertificationStatus.Certified)
        {
            throw new ConflictException(
                $"MechanicAnalysis {analysis.Id} is already certified.");
        }

        var certifiedAt = DateTimeOffset.UtcNow;

        analysis.CertifyViaOverride(request.Reason, request.UserId, certifiedAt);
        analysis.RaiseOverrideCertificationEvent(certifiedAt);

        _analysisRepository.Update(analysis);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        MeepleAiMetrics.Overrides.Add(1);

        _logger.LogInformation(
            "Admin override certification applied to MechanicAnalysis {AnalysisId} " +
            "(SharedGame {SharedGameId}) by user {UserId} at {CertifiedAt}.",
            analysis.Id,
            analysis.SharedGameId,
            request.UserId,
            certifiedAt);

        return Unit.Value;
    }
}
