using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="ApproveMechanicAnalysisCommand"/> (ISSUE-524 / M1.2 follow-up,
/// ADR-051).
/// </summary>
/// <remarks>
/// <para>
/// Loads the aggregate via <see cref="IMechanicAnalysisRepository.GetByIdWithClaimsIgnoringFiltersAsync"/>
/// because suppression is orthogonal to the lifecycle and the approval invariant must inspect
/// every claim's status.
/// </para>
/// <para>
/// Domain invariants surface as <see cref="InvalidMechanicAnalysisStateException"/> (source state
/// is not <c>InReview</c>) or <see cref="InvalidOperationException"/> (no claims, or not all
/// claims <c>Approved</c>). Both map to <see cref="ConflictException"/> (HTTP 409).
/// <see cref="DbUpdateConcurrencyException"/> (optimistic concurrency via PostgreSQL <c>xmin</c>)
/// also maps to 409 with a retry hint.
/// </para>
/// </remarks>
internal sealed class ApproveMechanicAnalysisCommandHandler
    : ICommandHandler<ApproveMechanicAnalysisCommand, MechanicAnalysisLifecycleResponseDto>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApproveMechanicAnalysisCommandHandler> _logger;

    public ApproveMechanicAnalysisCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ApproveMechanicAnalysisCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicAnalysisLifecycleResponseDto> Handle(
        ApproveMechanicAnalysisCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysis = await _analysisRepository
            .GetByIdWithClaimsIgnoringFiltersAsync(request.AnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            throw new NotFoundException(
                resourceType: "MechanicAnalysis",
                resourceId: request.AnalysisId.ToString());
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            analysis.Approve(request.ReviewerId, utcNow);
        }
        catch (InvalidMechanicAnalysisStateException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
            // Raised when claims are missing OR not all Approved (domain invariant).
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
                "Optimistic concurrency failure approving MechanicAnalysis {AnalysisId}.",
                request.AnalysisId);

            throw new ConflictException(
                "Mechanic analysis was modified by another operation. Please retry.",
                ex);
        }

        _logger.LogInformation(
            "MechanicAnalysis {AnalysisId} approved (published) by admin {ReviewerId}.",
            analysis.Id,
            request.ReviewerId);

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
