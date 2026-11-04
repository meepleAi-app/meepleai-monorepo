using Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;

/// <summary>
/// Handler for GetAgentDefinitionStatsQuery.
/// Issue #3708: Aggregates statistics for AgentDefinitions in AI Lab.
/// </summary>
internal sealed class GetAgentDefinitionStatsQueryHandler
    : IQueryHandler<GetAgentDefinitionStatsQuery, AgentDefinitionStatsResult>
{
    private readonly IAgentDefinitionRepository _repository;

    public GetAgentDefinitionStatsQueryHandler(IAgentDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AgentDefinitionStatsResult> Handle(
        GetAgentDefinitionStatsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Get all definitions (or active only)
        var definitions = request.ActiveOnly
            ? await _repository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false)
            : await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Calculate aggregate stats
        var totalDefinitions = definitions.Count;
        var activeDefinitions = definitions.Count(d => d.IsActive);
        var inactiveDefinitions = totalDefinitions - activeDefinitions;

        // Issue #4138: a distribution-by-type stood here. With one system agent and no
        // type field there is no axis to distribute over - it would be a single bucket.
        // No frontend read it (`grep -rn distributionByType apps/web/src` → nothing).

        // Recent definitions (last 10)
        var recentDefinitions = definitions
            .OrderByDescending(d => d.CreatedAt)
            .Take(10)
            .Select(d => new AgentDefinitionSummary
            {
                Id = d.Id,
                Name = d.Name,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt
            })
            .ToList();

        // Date range
        var oldestCreatedAt = definitions.Count > 0 ? definitions.Min(d => d.CreatedAt) : (DateTime?)null;
        var newestCreatedAt = definitions.Count > 0 ? definitions.Max(d => d.CreatedAt) : (DateTime?)null;

        return new AgentDefinitionStatsResult
        {
            TotalDefinitions = totalDefinitions,
            ActiveDefinitions = activeDefinitions,
            InactiveDefinitions = inactiveDefinitions,
            RecentDefinitions = recentDefinitions,
            OldestCreatedAt = oldestCreatedAt,
            NewestCreatedAt = newestCreatedAt
        };
    }
}
