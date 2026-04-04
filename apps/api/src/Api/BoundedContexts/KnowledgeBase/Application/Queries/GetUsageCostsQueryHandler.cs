using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Aggregates LLM cost data broken down by model, request source, and user tier.
/// Issue #5080: Admin usage page — cost breakdown panel.
/// </summary>
internal sealed class GetUsageCostsQueryHandler
    : IRequestHandler<GetUsageCostsQuery, UsageCostsDto>
{
    private readonly ILlmRequestLogRepository _repository;

    public GetUsageCostsQueryHandler(ILlmRequestLogRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<UsageCostsDto> Handle(
        GetUsageCostsQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var from = request.Period switch
        {
            "30d" => now.AddDays(-30),
            "1d" => now.AddDays(-1),
            _ => now.AddDays(-7)   // default "7d"
        };

        var (byModel, bySource, byTier, totalCostUsd, totalRequests) =
            await _repository.GetCostBreakdownAsync(from, now, cancellationToken).ConfigureAwait(false);

        return new UsageCostsDto(
            ByModel: byModel.Select(x => new ModelCostDto(x.ModelId, x.CostUsd, x.Requests, x.TotalTokens)).ToList(),
            BySource: bySource.Select(x => new SourceCostDto(x.Source, x.CostUsd, x.Requests)).ToList(),
            ByTier: byTier.Select(x => new TierCostDto(x.Tier, x.CostUsd, x.Requests)).ToList(),
            TotalCostUsd: totalCostUsd,
            TotalRequests: totalRequests,
            Period: request.Period
        );
    }
}
