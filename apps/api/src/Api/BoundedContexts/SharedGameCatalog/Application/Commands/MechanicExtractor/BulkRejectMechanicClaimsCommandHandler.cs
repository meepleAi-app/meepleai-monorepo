using System.Diagnostics;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="BulkRejectMechanicClaimsCommand"/> (#526 ME-M1.4).
/// </summary>
/// <remarks>
/// <para>
/// Rejects an explicit set of claims (decision D3) by dispatching <c>analysis.RejectClaim(...)</c>
/// for each, applying the shared <c>Reason</c>. All transitions land in one
/// <c>SaveChangesAsync</c>, so a partial failure (e.g. concurrency clash, or the parent analysis
/// not being <c>InReview</c>) rolls back the whole batch.
/// </para>
/// <para>
/// A claim id that does not exist on the analysis fails the batch with 404 (the client sent a
/// stale id). Claims already <c>Rejected</c> are skipped silently (idempotent) and surfaced via
/// <c>SkippedAlreadyRejectedCount</c>.
/// </para>
/// </remarks>
internal sealed class BulkRejectMechanicClaimsCommandHandler
    : ICommandHandler<BulkRejectMechanicClaimsCommand, BulkRejectMechanicClaimsResponseDto>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BulkRejectMechanicClaimsCommandHandler> _logger;

    public BulkRejectMechanicClaimsCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<BulkRejectMechanicClaimsCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkRejectMechanicClaimsResponseDto> Handle(
        BulkRejectMechanicClaimsCommand request,
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

        // De-duplicate the requested ids, then fail the batch if any does not exist (stale client).
        var requestedIds = request.ClaimIds.Distinct().ToList();
        var existingIds = analysis.Claims.Select(c => c.Id).ToHashSet();
        // Explicit Count check (not FirstOrDefault's Guid.Empty sentinel, which would
        // conflate "nothing missing" with "Guid.Empty was requested and is missing").
        var missingIds = requestedIds.Where(id => !existingIds.Contains(id)).ToList();
        if (missingIds.Count > 0)
        {
            throw new NotFoundException(
                resourceType: "MechanicClaim",
                resourceId: missingIds[0].ToString());
        }

        var requestedSet = requestedIds.ToHashSet();
        var targetClaims = analysis.Claims.Where(c => requestedSet.Contains(c.Id)).ToList();
        var toRejectIds = targetClaims
            .Where(c => c.Status != MechanicClaimStatus.Rejected)
            .Select(c => c.Id)
            .ToList();
        var skippedAlreadyRejectedCount = targetClaims.Count - toRejectIds.Count;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            foreach (var claimId in toRejectIds)
            {
                analysis.RejectClaim(claimId, request.ReviewerId, request.Reason, utcNow);
            }
        }
        catch (InvalidMechanicAnalysisStateException ex)
        {
            // Parent analysis is not InReview — fail the whole batch.
            throw new ConflictException(ex.Message, ex);
        }

        if (toRejectIds.Count > 0)
        {
            _analysisRepository.Update(analysis);

            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Optimistic concurrency failure bulk-rejecting claims on MechanicAnalysis {AnalysisId}.",
                    request.AnalysisId);

                throw new ConflictException(
                    "Mechanic analysis was modified by another operation. Please retry.",
                    ex);
            }
        }

        _logger.LogInformation(
            "Bulk-rejected {RejectedCount} claims on MechanicAnalysis {AnalysisId} (skipped {SkippedAlreadyRejectedCount} already-rejected) by admin {ReviewerId}.",
            toRejectIds.Count,
            analysis.Id,
            skippedAlreadyRejectedCount,
            request.ReviewerId);

        MeepleAiMetrics.MechanicReviewBulkActions.Add(1, new TagList { { "action", "bulk_reject" } });

        var claims = analysis.Claims
            .OrderBy(c => c.Section)
            .ThenBy(c => c.DisplayOrder)
            .Select(c => new MechanicClaimDto(
                Id: c.Id,
                AnalysisId: analysis.Id,
                Section: c.Section,
                Text: c.Text,
                DisplayOrder: c.DisplayOrder,
                Status: c.Status,
                ReviewedBy: c.ReviewedBy,
                ReviewedAt: c.ReviewedAt,
                RejectionNote: c.RejectionNote,
                ReviewNote: c.ReviewNote,
                Citations: c.Citations
                    .OrderBy(citation => citation.DisplayOrder)
                    .Select(citation => new MechanicCitationDto(
                        Id: citation.Id,
                        PdfPage: citation.PdfPage,
                        Quote: citation.Quote,
                        DisplayOrder: citation.DisplayOrder))
                    .ToList(),
                Validations: MechanicClaimValidations.FromDomain(c)))
            .ToList();

        return new BulkRejectMechanicClaimsResponseDto(
            RejectedCount: toRejectIds.Count,
            SkippedAlreadyRejectedCount: skippedAlreadyRejectedCount,
            Claims: claims);
    }
}
