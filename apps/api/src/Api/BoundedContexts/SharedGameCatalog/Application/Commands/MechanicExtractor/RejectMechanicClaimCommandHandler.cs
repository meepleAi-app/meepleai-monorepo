using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="RejectMechanicClaimCommand"/> (ISSUE-584).
/// </summary>
/// <remarks>
/// Mirrors <see cref="ApproveMechanicClaimCommandHandler"/> but calls
/// <c>analysis.RejectClaim(claimId, reviewerId, note, utcNow)</c>. 404/409 mapping identical.
/// </remarks>
internal sealed class RejectMechanicClaimCommandHandler
    : ICommandHandler<RejectMechanicClaimCommand, MechanicClaimDto>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RejectMechanicClaimCommandHandler> _logger;

    public RejectMechanicClaimCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<RejectMechanicClaimCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicClaimDto> Handle(
        RejectMechanicClaimCommand request,
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

        try
        {
            analysis.RejectClaim(request.ClaimId, request.ReviewerId, request.Note, utcNow);
        }
        catch (InvalidMechanicAnalysisStateException ex)
        {
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
                "Optimistic concurrency failure rejecting MechanicClaim {ClaimId} on MechanicAnalysis {AnalysisId}.",
                request.ClaimId,
                request.AnalysisId);

            throw new ConflictException(
                "Mechanic analysis was modified by another operation. Please retry.",
                ex);
        }

        var claim = analysis.Claims.First(c => c.Id == request.ClaimId);

        _logger.LogInformation(
            "MechanicClaim {ClaimId} on MechanicAnalysis {AnalysisId} rejected by admin {ReviewerId}.",
            claim.Id,
            analysis.Id,
            request.ReviewerId);

        return new MechanicClaimDto(
            Id: claim.Id,
            AnalysisId: analysis.Id,
            Section: claim.Section,
            Text: claim.Text,
            DisplayOrder: claim.DisplayOrder,
            Status: claim.Status,
            ReviewedBy: claim.ReviewedBy,
            ReviewedAt: claim.ReviewedAt,
            RejectionNote: claim.RejectionNote,
            Citations: claim.Citations
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new MechanicCitationDto(
                    Id: c.Id,
                    PdfPage: c.PdfPage,
                    Quote: c.Quote,
                    DisplayOrder: c.DisplayOrder))
                .ToList());
    }
}
