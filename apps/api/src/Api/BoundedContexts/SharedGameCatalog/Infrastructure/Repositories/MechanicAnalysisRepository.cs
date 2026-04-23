using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for the <see cref="MechanicAnalysis"/> aggregate (ADR-051 / M1.1).
/// </summary>
/// <remarks>
/// Audit-via-repository pattern: before clearing domain events via <see cref="RepositoryBase.CollectDomainEvents"/>,
/// the repository inspects the aggregate's pending events and synthesizes corresponding rows in
/// <c>mechanic_status_audit</c> / <c>mechanic_suppression_audit</c> into the same <see cref="DbContext"/>.
/// Because all inserts share the same <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>
/// they commit atomically (plan §2.3 atomicity invariant), without requiring a SaveChanges interceptor.
/// </remarks>
internal sealed class MechanicAnalysisRepository : RepositoryBase, IMechanicAnalysisRepository
{
    public MechanicAnalysisRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(MechanicAnalysis analysis, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var entity = MapToEntity(analysis);
        await DbContext.MechanicAnalyses.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        SynthesizeAuditRows(analysis);
        CollectDomainEvents(analysis);
    }

    public async Task<MechanicAnalysis?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicAnalyses
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity, claims: Array.Empty<MechanicClaimEntity>());
    }

    public async Task<MechanicAnalysis?> GetByIdWithClaimsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicAnalyses
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Claims)
                .ThenInclude(c => c.Citations)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity, entity.Claims);
    }

    public async Task<MechanicAnalysis?> GetByIdIgnoringFiltersAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicAnalyses
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity, claims: Array.Empty<MechanicClaimEntity>());
    }

    public async Task<MechanicAnalysis?> GetByIdWithClaimsIgnoringFiltersAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicAnalyses
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AsSplitQuery()
            .Include(a => a.Claims)
                .ThenInclude(c => c.Citations)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity, entity.Claims);
    }

    public async Task<MechanicAnalysis?> GetPublishedForSharedGameAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.MechanicAnalyses
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Claims)
                .ThenInclude(c => c.Citations)
            .Where(a => a.SharedGameId == sharedGameId
                        && a.Status == (int)MechanicAnalysisStatus.Published)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity, entity.Claims);
    }

    public async Task<IReadOnlyList<MechanicAnalysis>> GetReviewQueueAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.MechanicAnalyses
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AsSplitQuery()
            .Include(a => a.Claims)
                .ThenInclude(c => c.Citations)
            .Where(a => a.Status == (int)MechanicAnalysisStatus.InReview)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(e => MapToDomain(e, e.Claims)).ToList();
    }

    public async Task<MechanicAnalysis?> FindByPromptVersionAsync(
        Guid sharedGameId,
        Guid pdfDocumentId,
        string promptVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(promptVersion))
        {
            throw new ArgumentException("PromptVersion is required.", nameof(promptVersion));
        }

        // IgnoreQueryFilters so suppressed rows still block T7 idempotency collisions
        // (a takedown on one row shouldn't silently spawn a duplicate).
        // Exclude Rejected (status = 3) so operators can retry the same prompt version
        // after an auto-rejection (cost cap, LLM failure).
        var entity = await DbContext.MechanicAnalyses
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AsSplitQuery()
            .Include(a => a.Claims)
                .ThenInclude(c => c.Citations)
            .Where(a => a.SharedGameId == sharedGameId
                        && a.PdfDocumentId == pdfDocumentId
                        && a.PromptVersion == promptVersion
                        && a.Status != (int)MechanicAnalysisStatus.Rejected)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity, entity.Claims);
    }

    public void Update(MechanicAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var entity = MapToEntity(analysis);
        DbContext.MechanicAnalyses.Update(entity);

        SynthesizeAuditRows(analysis);
        CollectDomainEvents(analysis);
    }

    // === Audit synthesis ===

    /// <summary>
    /// Projects pending domain events onto audit tables in the same DbContext so the audit rows
    /// are written inside the same SaveChangesAsync transaction as the aggregate state change.
    /// </summary>
    private void SynthesizeAuditRows(MechanicAnalysis analysis)
    {
        foreach (var evt in analysis.DomainEvents)
        {
            switch (evt)
            {
                case MechanicAnalysisStatusChangedEvent statusEvt:
                    DbContext.MechanicStatusAudits.Add(new MechanicStatusAuditEntity
                    {
                        Id = Guid.NewGuid(),
                        AnalysisId = statusEvt.AnalysisId,
                        FromStatus = (int)statusEvt.FromStatus,
                        ToStatus = (int)statusEvt.ToStatus,
                        ActorId = statusEvt.ActorId,
                        Note = statusEvt.Note,
                        OccurredAt = statusEvt.OccurredAt
                    });
                    break;

                case MechanicAnalysisSuppressedEvent suppressedEvt:
                    DbContext.MechanicSuppressionAudits.Add(new MechanicSuppressionAuditEntity
                    {
                        Id = Guid.NewGuid(),
                        AnalysisId = suppressedEvt.AnalysisId,
                        IsSuppressed = true,
                        ActorId = suppressedEvt.ActorId,
                        Reason = suppressedEvt.Reason,
                        RequestSource = (int)suppressedEvt.RequestSource,
                        RequestedAt = suppressedEvt.RequestedAt,
                        OccurredAt = suppressedEvt.OccurredAt
                    });
                    break;

                case MechanicAnalysisUnsuppressedEvent unsuppressedEvt:
                    DbContext.MechanicSuppressionAudits.Add(new MechanicSuppressionAuditEntity
                    {
                        Id = Guid.NewGuid(),
                        AnalysisId = unsuppressedEvt.AnalysisId,
                        IsSuppressed = false,
                        ActorId = unsuppressedEvt.ActorId,
                        Reason = unsuppressedEvt.Reason,
                        RequestSource = null,
                        RequestedAt = null,
                        OccurredAt = unsuppressedEvt.OccurredAt
                    });
                    break;

                // MechanicAnalysisCostCapOverriddenEvent: no dedicated audit table in M1.1;
                // the event is still dispatched by the base class for downstream handlers (admin UI, logs).
            }
        }
    }

    // === Mapping ===

    private static MechanicAnalysis MapToDomain(
        MechanicAnalysisEntity entity,
        IEnumerable<MechanicClaimEntity> claims)
    {
        var domainClaims = claims.Select(MapClaimToDomain).ToList();

        return MechanicAnalysis.Reconstitute(
            id: entity.Id,
            sharedGameId: entity.SharedGameId,
            pdfDocumentId: entity.PdfDocumentId,
            promptVersion: entity.PromptVersion,
            status: (MechanicAnalysisStatus)entity.Status,
            createdBy: entity.CreatedBy,
            createdAt: entity.CreatedAt,
            reviewedBy: entity.ReviewedBy,
            reviewedAt: entity.ReviewedAt,
            rejectionReason: entity.RejectionReason,
            totalTokensUsed: entity.TotalTokensUsed,
            estimatedCostUsd: entity.EstimatedCostUsd,
            modelUsed: entity.ModelUsed,
            provider: entity.Provider,
            costCapUsd: entity.CostCapUsd,
            costCapOverrideAt: entity.CostCapOverrideAt,
            costCapOverrideBy: entity.CostCapOverrideBy,
            costCapOverrideReason: entity.CostCapOverrideReason,
            isSuppressed: entity.IsSuppressed,
            suppressedAt: entity.SuppressedAt,
            suppressedBy: entity.SuppressedBy,
            suppressionReason: entity.SuppressionReason,
            suppressionRequestedAt: entity.SuppressionRequestedAt,
            suppressionRequestSource: entity.SuppressionRequestSource is null
                ? null
                : (SuppressionRequestSource)entity.SuppressionRequestSource.Value,
            claims: domainClaims,
            xminVersion: entity.Xmin);
    }

    private static MechanicClaim MapClaimToDomain(MechanicClaimEntity entity)
    {
        var citations = entity.Citations.Select(MapCitationToDomain).ToList();

        return MechanicClaim.Reconstitute(
            id: entity.Id,
            analysisId: entity.AnalysisId,
            section: (MechanicSection)entity.Section,
            text: entity.Text,
            displayOrder: entity.DisplayOrder,
            status: (MechanicClaimStatus)entity.Status,
            reviewedBy: entity.ReviewedBy,
            reviewedAt: entity.ReviewedAt,
            rejectionNote: entity.RejectionNote,
            citations: citations);
    }

    private static MechanicCitation MapCitationToDomain(MechanicCitationEntity entity)
    {
        return MechanicCitation.Reconstitute(
            id: entity.Id,
            claimId: entity.ClaimId,
            pdfPage: entity.PdfPage,
            quote: entity.Quote,
            chunkId: entity.ChunkId,
            displayOrder: entity.DisplayOrder);
    }

    private static MechanicAnalysisEntity MapToEntity(MechanicAnalysis analysis)
    {
        return new MechanicAnalysisEntity
        {
            Id = analysis.Id,
            SharedGameId = analysis.SharedGameId,
            PdfDocumentId = analysis.PdfDocumentId,
            PromptVersion = analysis.PromptVersion,
            Status = (int)analysis.Status,
            CreatedBy = analysis.CreatedBy,
            CreatedAt = analysis.CreatedAt,
            ReviewedBy = analysis.ReviewedBy,
            ReviewedAt = analysis.ReviewedAt,
            RejectionReason = analysis.RejectionReason,
            TotalTokensUsed = analysis.TotalTokensUsed,
            EstimatedCostUsd = analysis.EstimatedCostUsd,
            ModelUsed = analysis.ModelUsed,
            Provider = analysis.Provider,
            CostCapUsd = analysis.CostCapUsd,
            CostCapOverrideAt = analysis.CostCapOverrideAt,
            CostCapOverrideBy = analysis.CostCapOverrideBy,
            CostCapOverrideReason = analysis.CostCapOverrideReason,
            IsSuppressed = analysis.IsSuppressed,
            SuppressedAt = analysis.SuppressedAt,
            SuppressedBy = analysis.SuppressedBy,
            SuppressionReason = analysis.SuppressionReason,
            SuppressionRequestedAt = analysis.SuppressionRequestedAt,
            SuppressionRequestSource = analysis.SuppressionRequestSource is null
                ? null
                : (int)analysis.SuppressionRequestSource.Value,
            Xmin = analysis.XminVersion,
            Claims = analysis.Claims.Select(MapClaimToEntity).ToList()
        };
    }

    private static MechanicClaimEntity MapClaimToEntity(MechanicClaim claim)
    {
        return new MechanicClaimEntity
        {
            Id = claim.Id,
            AnalysisId = claim.AnalysisId,
            Section = (int)claim.Section,
            Text = claim.Text,
            DisplayOrder = claim.DisplayOrder,
            Status = (int)claim.Status,
            ReviewedBy = claim.ReviewedBy,
            ReviewedAt = claim.ReviewedAt,
            RejectionNote = claim.RejectionNote,
            Citations = claim.Citations.Select(MapCitationToEntity).ToList()
        };
    }

    private static MechanicCitationEntity MapCitationToEntity(MechanicCitation citation)
    {
        return new MechanicCitationEntity
        {
            Id = citation.Id,
            ClaimId = citation.ClaimId,
            PdfPage = citation.PdfPage,
            Quote = citation.Quote,
            ChunkId = citation.ChunkId,
            DisplayOrder = citation.DisplayOrder
        };
    }
}
