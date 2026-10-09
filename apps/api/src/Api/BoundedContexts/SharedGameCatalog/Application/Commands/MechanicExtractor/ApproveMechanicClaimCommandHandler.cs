using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="ApproveMechanicClaimCommand"/> (ISSUE-584).
/// </summary>
/// <remarks>
/// <para>
/// Loads the aggregate via <see cref="IMechanicAnalysisRepository.GetByIdWithClaimsIgnoringFiltersAsync"/>
/// because suppression is orthogonal to the lifecycle and per-claim review must work on
/// suppressed rows that are still in the moderation queue.
/// </para>
/// <para>
/// 404 mapping: missing parent analysis → <c>NotFoundException("MechanicAnalysis")</c>;
/// claim not part of the aggregate → <c>NotFoundException("MechanicClaim")</c>.
/// 409 mapping: parent not <c>InReview</c> → <see cref="InvalidMechanicAnalysisStateException"/>;
/// optimistic concurrency on parent <c>xmin</c> → <see cref="DbUpdateConcurrencyException"/>.
/// </para>
/// <para>
/// The structure in effect after the approval — the command's <c>Structure</c> when present, otherwise
/// the claim's current one (claims built by the parser never went through <c>SetClaimStructure</c>) —
/// must satisfy the override-graph invariants: a violation → 400 (<see cref="BadRequestException"/>),
/// nothing saved. A supplied structure is applied with the approval, so a Rejected claim can be
/// approved and edited in one call.
/// </para>
/// </remarks>
internal sealed class ApproveMechanicClaimCommandHandler
    : ICommandHandler<ApproveMechanicClaimCommand, MechanicClaimDto>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ApproveMechanicClaimCommandHandler> _logger;

    public ApproveMechanicClaimCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ApproveMechanicClaimCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicClaimDto> Handle(
        ApproveMechanicClaimCommand request,
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

        if (analysis.Claims.All(c => c.Id != request.ClaimId))
        {
            throw new NotFoundException(
                resourceType: "MechanicClaim",
                resourceId: request.ClaimId.ToString());
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        // The supplied structure (or, without one, the claim's current structure) is validated against
        // the override graph BEFORE the approval, then applied together with it: a previously Rejected
        // claim can be approved and corrected in one call, and a reviewer can fix an invalid proposal.
        // Nothing is persisted until SaveChanges, so a 400/409 here leaves no partial state.
        try
        {
            analysis.ApproveClaim(request.ClaimId, request.ReviewerId, utcNow, request.Note, request.Structure?.ToDomain());
        }
        catch (InvalidMechanicAnalysisStateException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }
        catch (ArgumentException ex)
        {
            throw new BadRequestException(ex.Message, ex);
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
                "Optimistic concurrency failure approving MechanicClaim {ClaimId} on MechanicAnalysis {AnalysisId}.",
                request.ClaimId,
                request.AnalysisId);

            throw new ConflictException(
                "Mechanic analysis was modified by another operation. Please retry.",
                ex);
        }

        var claim = analysis.Claims.First(c => c.Id == request.ClaimId);

        _logger.LogInformation(
            "MechanicClaim {ClaimId} on MechanicAnalysis {AnalysisId} approved by admin {ReviewerId}.",
            claim.Id,
            analysis.Id,
            request.ReviewerId);

        return MechanicClaimDtoMapper.FromDomain(claim, analysis.Id);
    }
}
