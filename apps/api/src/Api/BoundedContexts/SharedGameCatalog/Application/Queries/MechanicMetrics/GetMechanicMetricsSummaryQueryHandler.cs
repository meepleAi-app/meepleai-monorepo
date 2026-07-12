using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicMetrics;

internal sealed class GetMechanicMetricsSummaryQueryHandler
    : IQueryHandler<GetMechanicMetricsSummaryQuery, MechanicMetricsSummaryDto>
{
    // MechanicAnalysisStatus ints: Draft=0, InReview=1, Published=2, Rejected=3, PartiallyExtracted=4.
    private const int StatusInReview = 1;
    private const int StatusPublished = 2;
    private const int StatusRejected = 3;
    private const int StatusPartiallyExtracted = 4;

    private readonly MeepleAiDbContext _db;

    public GetMechanicMetricsSummaryQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<MechanicMetricsSummaryDto> Handle(
        GetMechanicMetricsSummaryQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Query filter already excludes IsSuppressed. AsNoTracking per PERF-05 (read-only analytics).
        var query = _db.MechanicAnalyses.AsNoTracking().AsQueryable();

        if (request.GameId is Guid gameId)
        {
            query = query.Where(a => a.SharedGameId == gameId);
        }
        if (request.ReviewerId is Guid reviewerId)
        {
            query = query.Where(a => a.ReviewedBy == reviewerId);
        }
        if (request.StartDate is DateTime start)
        {
            query = query.Where(a => a.CreatedAt >= start);
        }
        if (request.EndDate is DateTime end)
        {
            query = query.Where(a => a.CreatedAt <= end);
        }

        // Materialize the minimal projection for the filtered set — mechanic analyses are modest in
        // volume, and review-time (TimeSpan.TotalHours) + rejection-reason grouping are cleanest in memory.
        var rows = await query
            .Select(a => new Row(a.Status, a.EstimatedCostUsd, a.CreatedAt, a.ReviewedAt, a.RejectionReason, a.CreatedBy, a.ReviewedBy))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = rows.Count;
        var published = rows.Count(r => r.Status == StatusPublished);
        var rejected = rows.Count(r => r.Status is StatusRejected or StatusPartiallyExtracted);
        var inReview = rows.Count(r => r.Status == StatusInReview);
        var totalCost = rows.Sum(r => r.Cost);
        var averageCost = total > 0 ? totalCost / total : 0m;

        // Review time reflects HUMAN review throughput only. System auto-transitions
        // (AutoRejectFromDraft, MarkAsPartiallyExtracted) stamp ReviewedAt≈CreatedAt in the same
        // pipeline run using the analysis' own CreatedBy as actor, so they'd drag the average toward
        // zero. Include only terminal human decisions (Published/Rejected) reviewed by someone other
        // than the creator (Partial=4 is always a system transition, so it's excluded by status).
        var humanReviewed = rows
            .Where(r => r.ReviewedAt.HasValue
                && r.Status is StatusPublished or StatusRejected
                && r.ReviewedBy.HasValue && r.ReviewedBy.Value != r.CreatedBy)
            .ToList();
        double? averageReviewHours = humanReviewed.Count > 0
            ? humanReviewed.Average(r => (r.ReviewedAt!.Value - r.CreatedAt).TotalHours)
            : null;

        // Approval rate over reviewed terminal states (Published vs Rejected/Partial).
        var approvalDenominator = published + rejected;
        var approvalRate = approvalDenominator > 0 ? (double)published / approvalDenominator * 100.0 : 0.0;

        var rejectionBreakdown = rows
            .Where(r => r.Status is StatusRejected or StatusPartiallyExtracted && !string.IsNullOrWhiteSpace(r.RejectionReason))
            .GroupBy(r => r.RejectionReason!, StringComparer.Ordinal)
            .Select(g => new RejectionReasonCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Reason, StringComparer.Ordinal)
            .ToList();

        return new MechanicMetricsSummaryDto(
            totalCost, total, published, rejected, inReview,
            averageCost, averageReviewHours, approvalRate, rejectionBreakdown);
    }

    private sealed record Row(
        int Status, decimal Cost, DateTime CreatedAt, DateTime? ReviewedAt, string? RejectionReason,
        Guid CreatedBy, Guid? ReviewedBy);
}
