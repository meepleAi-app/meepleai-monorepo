using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Handler for model performance query.
/// Groups AgentTestResultEntity by model name and computes aggregated metrics.
/// </summary>
internal class GetModelPerformanceQueryHandler : IQueryHandler<GetModelPerformanceQuery, ModelPerformanceDto>
{
    private readonly MeepleAiDbContext _db;

    public GetModelPerformanceQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<ModelPerformanceDto> Handle(GetModelPerformanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rawModels = await _db.Set<AgentTestResultEntity>().AsNoTracking()
            .GroupBy(r => r.ModelUsed)
            .Select(g => new
            {
                ModelName = g.Key,
                Invocations = g.Count(),
                AvgLatencyMs = g.Average(r => (double)r.LatencyMs),
                TotalCost = g.Sum(r => r.CostEstimate),
                AvgConfidence = g.Average(r => r.ConfidenceScore)
            })
            .OrderByDescending(m => m.Invocations)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var models = rawModels.Select(m => new ModelPerformanceItemDto(
            m.ModelName,
            m.Invocations,
            Math.Round(m.AvgLatencyMs, 2),
            m.TotalCost,
            Math.Round(m.AvgConfidence, 4))).ToList();

        return new ModelPerformanceDto(Models: models);
    }
}
