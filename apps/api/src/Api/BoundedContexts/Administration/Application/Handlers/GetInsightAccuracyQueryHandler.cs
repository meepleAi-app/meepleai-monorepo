using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetInsightAccuracyQuery.
/// Issue #4124: Calculates insight accuracy from user feedback data.
/// </summary>
internal sealed class GetInsightAccuracyQueryHandler : IRequestHandler<GetInsightAccuracyQuery, InsightAccuracyResponseDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public GetInsightAccuracyQueryHandler(MeepleAiDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<InsightAccuracyResponseDto> Handle(GetInsightAccuracyQuery request, CancellationToken cancellationToken)
    {
        // Database-level aggregation for efficiency (avoids loading all rows into memory)
        var stats = await _dbContext.Set<InsightFeedbackEntity>()
            .AsNoTracking()
            .GroupBy(f => f.InsightType)
            .Select(g => new
            {
                Type = g.Key,
                Total = g.Count(),
                Relevant = g.Count(f => f.IsRelevant)
            })
            .OrderByDescending(s => s.Total)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalFeedback = stats.Sum(s => s.Total);
        var relevantCount = stats.Sum(s => s.Relevant);
        var accuracy = totalFeedback > 0 ? (double)relevantCount / totalFeedback * 100 : 0;

        var byType = stats
            .Select(s => new InsightTypeAccuracyDto(
                Type: s.Type,
                Total: s.Total,
                Relevant: s.Relevant,
                AccuracyPercentage: s.Total > 0 ? (double)s.Relevant / s.Total * 100 : 0
            ))
            .ToList();

        return new InsightAccuracyResponseDto(
            TotalFeedback: totalFeedback,
            RelevantCount: relevantCount,
            AccuracyPercentage: Math.Round(accuracy, 1),
            ByType: byType,
            CalculatedAt: _timeProvider.GetUtcNow().UtcDateTime
        );
    }
}
