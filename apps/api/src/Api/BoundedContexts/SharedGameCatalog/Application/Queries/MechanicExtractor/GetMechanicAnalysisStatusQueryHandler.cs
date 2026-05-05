using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Handler for <see cref="GetMechanicAnalysisStatusQuery"/> (ISSUE-524 / M1.2).
/// Projects the aggregate + its section runs into a lightweight status DTO for admin polling.
/// Bypasses the <c>IsSuppressed</c> global query filter so moderators can inspect suppressed
/// analyses without losing visibility.
/// </summary>
internal sealed class GetMechanicAnalysisStatusQueryHandler
    : IQueryHandler<GetMechanicAnalysisStatusQuery, MechanicAnalysisStatusDto?>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetMechanicAnalysisStatusQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<MechanicAnalysisStatusDto?> Handle(
        GetMechanicAnalysisStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var analysis = await _dbContext.MechanicAnalyses
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == request.AnalysisId, cancellationToken)
            .ConfigureAwait(false);

        if (analysis is null)
        {
            return null;
        }

        var claimsCount = await _dbContext.MechanicClaims
            .AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(c => c.AnalysisId == request.AnalysisId, cancellationToken)
            .ConfigureAwait(false);

        var sectionRunEntities = await _dbContext.MechanicAnalysisSectionRuns
            .AsNoTracking()
            .Where(r => r.AnalysisId == request.AnalysisId)
            .OrderBy(r => r.RunOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var sectionRuns = sectionRunEntities.Select(MapSectionRun).ToList();

        return new MechanicAnalysisStatusDto(
            Id: analysis.Id,
            SharedGameId: analysis.SharedGameId,
            PdfDocumentId: analysis.PdfDocumentId,
            PromptVersion: analysis.PromptVersion,
            Status: (MechanicAnalysisStatus)analysis.Status,
            RejectionReason: analysis.RejectionReason,
            CreatedBy: analysis.CreatedBy,
            CreatedAt: analysis.CreatedAt,
            ReviewedBy: analysis.ReviewedBy,
            ReviewedAt: analysis.ReviewedAt,
            Provider: analysis.Provider,
            ModelUsed: analysis.ModelUsed,
            TotalTokensUsed: analysis.TotalTokensUsed,
            EstimatedCostUsd: analysis.EstimatedCostUsd,
            CostCapUsd: analysis.CostCapUsd,
            CostCapOverrideApplied: analysis.CostCapOverrideBy is not null,
            CostCapOverrideAt: analysis.CostCapOverrideAt,
            CostCapOverrideBy: analysis.CostCapOverrideBy,
            CostCapOverrideReason: analysis.CostCapOverrideReason,
            IsSuppressed: analysis.IsSuppressed,
            SuppressedAt: analysis.SuppressedAt,
            SuppressedBy: analysis.SuppressedBy,
            SuppressionReason: analysis.SuppressionReason,
            ClaimsCount: claimsCount,
            SectionRuns: sectionRuns);
    }

    private static MechanicSectionRunSummaryDto MapSectionRun(MechanicAnalysisSectionRunEntity entity) =>
        new(
            Section: entity.Section,
            RunOrder: entity.RunOrder,
            Provider: entity.Provider,
            ModelUsed: entity.ModelUsed,
            PromptTokens: entity.PromptTokens,
            CompletionTokens: entity.CompletionTokens,
            TotalTokens: entity.TotalTokens,
            EstimatedCostUsd: entity.EstimatedCostUsd,
            LatencyMs: entity.LatencyMs,
            Status: entity.Status,
            ErrorMessage: entity.ErrorMessage,
            StartedAt: entity.StartedAt,
            CompletedAt: entity.CompletedAt);
}
