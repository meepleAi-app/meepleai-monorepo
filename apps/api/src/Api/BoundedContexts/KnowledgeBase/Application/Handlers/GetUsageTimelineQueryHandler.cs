using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Builds the timeline dataset for the request timeline chart.
/// Returns per-source hourly or daily buckets over the requested period.
/// Issue #5078: Admin usage page — request timeline chart.
/// </summary>
internal sealed class GetUsageTimelineQueryHandler
    : IRequestHandler<GetUsageTimelineQuery, UsageTimelineDto>
{
    private readonly ILlmRequestLogRepository _repository;

    public GetUsageTimelineQueryHandler(ILlmRequestLogRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<UsageTimelineDto> Handle(
        GetUsageTimelineQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (from, groupByHour) = request.Period switch
        {
            "7d"  => (now.AddDays(-7), false),
            "30d" => (now.AddDays(-30), false),
            _     => (now.AddHours(-24), true)   // default "24h"
        };

        var raw = await _repository
            .GetTimelineAsync(from, now, groupByHour, cancellationToken)
            .ConfigureAwait(false);

        // Pivot: group by bucket, then spread sources across columns
        var byBucket = raw
            .GroupBy(x => x.Bucket)
            .Select(g =>
            {
                int CountFor(string src) =>
                    g.Where(x => string.Equals(x.Source, src, StringComparison.Ordinal)).Sum(x => x.Count);

                return new TimelineBucketDto(
                    Bucket:          g.Key,
                    Manual:          CountFor("Manual"),
                    RagPipeline:     CountFor("RagPipeline"),
                    EventDriven:     CountFor("EventDriven"),
                    AutomatedTest:   CountFor("AutomatedTest"),
                    AgentTask:       CountFor("AgentTask"),
                    AdminOperation:  CountFor("AdminOperation"),
                    TotalCostUsd:    g.Sum(x => x.CostUsd)
                );
            })
            .OrderBy(b => b.Bucket)
            .ToList();

        return new UsageTimelineDto(
            Buckets:       byBucket,
            Period:        request.Period,
            GroupedByHour: groupByHour,
            TotalRequests: byBucket.Sum(b =>
                b.Manual + b.RagPipeline + b.EventDriven +
                b.AutomatedTest + b.AgentTask + b.AdminOperation),
            TotalCostUsd:  byBucket.Sum(b => b.TotalCostUsd)
        );
    }
}
