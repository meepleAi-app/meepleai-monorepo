using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="SuppressMechanicAnalysisCommand"/> (ISSUE-524 / M1.2 follow-up,
/// ADR-051 T5).
/// </summary>
/// <remarks>
/// <para>
/// Uses <see cref="IMechanicAnalysisRepository.GetByIdIgnoringFiltersAsync"/> so an already-
/// suppressed row still resolves — otherwise the global query filter would hide the row and
/// the endpoint would return 404 instead of the correct 409 for the double-suppress case.
/// Claims are NOT loaded because suppression does not touch child state.
/// </para>
/// <para>
/// <see cref="InvalidOperationException"/> from the aggregate (row already suppressed) maps to
/// <see cref="ConflictException"/> (HTTP 409). <see cref="DbUpdateConcurrencyException"/> from
/// <c>xmin</c> optimistic concurrency also maps to 409 with a retry hint.
/// </para>
/// </remarks>
internal sealed class SuppressMechanicAnalysisCommandHandler
    : ICommandHandler<SuppressMechanicAnalysisCommand, MechanicAnalysisLifecycleResponseDto>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SuppressMechanicAnalysisCommandHandler> _logger;

    public SuppressMechanicAnalysisCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<SuppressMechanicAnalysisCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicAnalysisLifecycleResponseDto> Handle(
        SuppressMechanicAnalysisCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysis = await _analysisRepository
            .GetByIdIgnoringFiltersAsync(request.AnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            throw new NotFoundException(
                resourceType: "MechanicAnalysis",
                resourceId: request.AnalysisId.ToString());
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        // Normalize any incoming Unspecified timestamps to UTC to keep the audit column honest.
        DateTime? normalizedRequestedAt = request.RequestedAt.HasValue
            ? DateTime.SpecifyKind(request.RequestedAt.Value, DateTimeKind.Utc)
            : null;

        try
        {
            analysis.Suppress(
                actorId: request.ActorId,
                reason: request.Reason,
                requestSource: request.RequestSource,
                requestedAt: normalizedRequestedAt,
                utcNow: utcNow);
        }
        catch (InvalidOperationException ex)
        {
            // Raised when the aggregate is already suppressed (orthogonal kill-switch invariant).
            throw new ConflictException(ex.Message, ex);
        }

        _analysisRepository.Update(analysis);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "Optimistic concurrency failure suppressing MechanicAnalysis {AnalysisId}.",
                request.AnalysisId);

            throw new ConflictException(
                "Mechanic analysis was modified by another operation. Please retry.",
                ex);
        }

        _logger.LogInformation(
            "MechanicAnalysis {AnalysisId} suppressed by admin {ActorId} (source: {RequestSource}).",
            analysis.Id,
            request.ActorId,
            request.RequestSource);

        return new MechanicAnalysisLifecycleResponseDto(
            Id: analysis.Id,
            Status: analysis.Status,
            ReviewedBy: analysis.ReviewedBy,
            ReviewedAt: analysis.ReviewedAt,
            IsSuppressed: analysis.IsSuppressed,
            SuppressedAt: analysis.SuppressedAt,
            SuppressedBy: analysis.SuppressedBy,
            SuppressionReason: analysis.SuppressionReason,
            SuppressionRequestSource: analysis.SuppressionRequestSource);
    }
}
