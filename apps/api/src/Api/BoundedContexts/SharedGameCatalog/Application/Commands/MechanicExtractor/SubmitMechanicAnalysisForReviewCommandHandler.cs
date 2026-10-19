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
/// Handler for <see cref="SubmitMechanicAnalysisForReviewCommand"/> (ISSUE-524 / M1.2
/// follow-up, ADR-051).
/// </summary>
/// <remarks>
/// <para>
/// The handler loads the aggregate with its claim graph via
/// <see cref="IMechanicAnalysisRepository.GetByIdWithClaimsIgnoringFiltersAsync"/> — suppression
/// is orthogonal to the lifecycle so suppressed rows must still resolve, and the domain needs
/// the claims collection to reset pending/rejected claims on a resubmission from
/// <c>Rejected</c>.
/// </para>
/// <para>
/// Domain invariants bubble up as <see cref="InvalidMechanicAnalysisStateException"/> (wrong
/// source state) or <see cref="InvalidOperationException"/> (no claims). Both map to
/// <see cref="ConflictException"/> (HTTP 409): the request is syntactically valid but conflicts
/// with aggregate state. <see cref="DbUpdateConcurrencyException"/> (optimistic concurrency via
/// PostgreSQL <c>xmin</c>) also maps to 409 with a retry hint.
/// </para>
/// <para>
/// A resubmission from <c>Rejected</c> re-enters the filtered unique index on (game, pdf, prompt):
/// when another analysis holds that slot the handler answers 409 saying what to do — before the
/// UPDATE, and again if the 23505 of a race slips past the check (#4144, see
/// <see cref="MechanicAnalysisPromptSlot"/>).
/// </para>
/// </remarks>
internal sealed class SubmitMechanicAnalysisForReviewCommandHandler
    : ICommandHandler<SubmitMechanicAnalysisForReviewCommand, MechanicAnalysisLifecycleResponseDto>
{
    private readonly IMechanicAnalysisRepository _analysisRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubmitMechanicAnalysisForReviewCommandHandler> _logger;

    public SubmitMechanicAnalysisForReviewCommandHandler(
        IMechanicAnalysisRepository analysisRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<SubmitMechanicAnalysisForReviewCommandHandler> logger)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MechanicAnalysisLifecycleResponseDto> Handle(
        SubmitMechanicAnalysisForReviewCommand request,
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

        // Rejected → InReview is the only transition that brings an analysis back inside the filtered
        // unique index (one non-Rejected analysis per game, pdf, prompt). If another analysis took the
        // slot meanwhile — typically one regenerated after this was rejected (#4144) — the UPDATE would
        // fail 23505 → 500. Say what to do instead.
        if (analysis.Status == MechanicAnalysisStatus.Rejected)
        {
            var holder = await _analysisRepository
                .FindByPromptVersionAsync(analysis.SharedGameId, analysis.PdfDocumentId, analysis.PromptVersion, cancellationToken)
                .ConfigureAwait(false);

            if (holder is not null && holder.Id != analysis.Id)
            {
                throw MechanicAnalysisPromptSlot.Occupied(
                    PromptSlotWrite.ResubmitForReview, holder.Id, holder.Status, holder.CreatedAt, analysis.PromptVersion);
            }
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            analysis.SubmitForReview(request.ActorId, utcNow);
        }
        catch (InvalidMechanicAnalysisStateException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }
        catch (InvalidOperationException ex)
        {
            // Raised when the aggregate has no claims (domain invariant).
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
                "Optimistic concurrency failure submitting MechanicAnalysis {AnalysisId} for review.",
                request.AnalysisId);

            throw new ConflictException(
                "Mechanic analysis was modified by another operation. Please retry.",
                ex);
        }
        catch (DbUpdateException ex) when (MechanicAnalysisPromptSlot.IsTaken(ex))
        {
            // The race past the guard above: another analysis took the slot between the check and
            // this UPDATE. Both outcomes below throw and end the request, and nothing saves in this
            // scope afterwards (the command is not [AuditableAction] and is only sent by its endpoint),
            // so the dirty change tracker is not cleaned here, unlike GenerateMechanicAnalysisCommandHandler,
            // which can return normally inside another handler's scope.
            var holder = await _analysisRepository
                .FindByPromptVersionAsync(analysis.SharedGameId, analysis.PdfDocumentId, analysis.PromptVersion, cancellationToken)
                .ConfigureAwait(false);

            if (holder is null || holder.Id == analysis.Id)
            {
                throw;
            }

            throw MechanicAnalysisPromptSlot.Occupied(
                PromptSlotWrite.ResubmitForReview, holder.Id, holder.Status, holder.CreatedAt, analysis.PromptVersion, ex);
        }

        _logger.LogInformation(
            "MechanicAnalysis {AnalysisId} submitted for review by admin {ActorId}.",
            analysis.Id,
            request.ActorId);

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
