using System.Diagnostics;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for <see cref="BulkApproveMechanicClaimsCommand"/> (ISSUE-584).
/// </summary>
/// <remarks>
/// <para>
/// Iterates every <c>Pending</c> claim and dispatches <c>analysis.ApproveClaim(...)</c>. All
/// transitions land in the same <c>SaveChangesAsync</c> call, so any partial failure (e.g.
/// concurrency clash) rolls back the whole batch.
/// </para>
/// <para>
/// <c>Approved</c> claims are skipped silently (idempotent). <c>Rejected</c> claims are
/// preserved — the reviewer must explicitly re-approve them via the per-claim endpoint after
/// addressing the rejection note. The response surfaces a <c>SkippedRejectedCount</c> so the
/// UI can warn that promotion to <c>Published</c> is still blocked.
/// </para>
/// <para>
/// D8 safety guard (#2782 FU-1): <c>Pending</c> claims carrying any real <c>fail</c> validation
/// (see <c>MechanicClaim.Validations</c>) are excluded from this implicit approve-all
/// sweep — an admin must not be able to one-click rubber-stamp a hallucination. Those claims
/// stay <c>Pending</c> and require explicit per-claim approval. The skipped-fail count is
/// log-only (no response DTO field) to keep the response contract and FE Zod schema unchanged.
/// </para>
/// </remarks>
internal sealed class BulkApproveMechanicClaimsCommandHandler
    : ICommandHandler<BulkApproveMechanicClaimsCommand, BulkApproveMechanicClaimsResponseDto>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BulkApproveMechanicClaimsCommandHandler> _logger;

    public BulkApproveMechanicClaimsCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<BulkApproveMechanicClaimsCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkApproveMechanicClaimsResponseDto> Handle(
        BulkApproveMechanicClaimsCommand request,
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

        var pendingClaimIds = analysis.Claims
            .Where(c => c.Status == MechanicClaimStatus.Pending
                        && !c.Validations.Any(v => string.Equals(v.Outcome, MechanicClaimValidationOutcomes.Fail, StringComparison.Ordinal)))
            .Select(c => c.Id)
            .ToList();

        var skippedFailFlaggedCount = analysis.Claims
            .Count(c => c.Status == MechanicClaimStatus.Pending
                        && c.Validations.Any(v => string.Equals(v.Outcome, MechanicClaimValidationOutcomes.Fail, StringComparison.Ordinal)));

        var skippedRejectedCount = analysis.Claims
            .Count(c => c.Status == MechanicClaimStatus.Rejected);

        if (skippedFailFlaggedCount > 0)
        {
            _logger.LogInformation(
                "BulkApprove skipped {Count} fail-flagged claim(s) for analysis {AnalysisId}",
                skippedFailFlaggedCount,
                analysis.Id);
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            foreach (var claimId in pendingClaimIds)
            {
                analysis.ApproveClaim(claimId, request.ReviewerId, utcNow);
            }
        }
        catch (InvalidMechanicAnalysisStateException ex)
        {
            // Parent analysis is not InReview — fail the whole batch.
            throw new ConflictException(ex.Message, ex);
        }

        if (pendingClaimIds.Count > 0)
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
                    "Optimistic concurrency failure bulk-approving claims on MechanicAnalysis {AnalysisId}.",
                    request.AnalysisId);

                throw new ConflictException(
                    "Mechanic analysis was modified by another operation. Please retry.",
                    ex);
            }
        }

        _logger.LogInformation(
            "Bulk-approved {ApprovedCount} claims on MechanicAnalysis {AnalysisId} (skipped {SkippedRejectedCount} rejected) by admin {ReviewerId}.",
            pendingClaimIds.Count,
            analysis.Id,
            skippedRejectedCount,
            request.ReviewerId);

        MeepleAiMetrics.MechanicReviewBulkActions.Add(1, new TagList { { "action", "bulk_approve" } });

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

        return new BulkApproveMechanicClaimsResponseDto(
            ApprovedCount: pendingClaimIds.Count,
            SkippedRejectedCount: skippedRejectedCount,
            Claims: claims);
    }
}
