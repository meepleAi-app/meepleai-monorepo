using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;

/// <summary>
/// Returns agent definitions ranked by distinct user count (Approach A — AgentSession proxy).
/// Limit is clamped to [1, 20].
///
/// MVP: Name, GameName and AgentType are empty strings because there is no AgentDefinition
/// entity joinable from AgentSessionEntity. Follow-up issue once the KB BC introduces
/// a proper install/agent-definition model.
/// Issue #728: Discover dashboard — "Top Agents" widget.
/// </summary>
internal sealed class GetTopAgentsHandler : IQueryHandler<GetTopAgentsQuery, IReadOnlyList<TopAgentDto>>
{
    private const int MaxLimit = 20;
    private const int MinLimit = 1;

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetTopAgentsHandler> _logger;

    public GetTopAgentsHandler(MeepleAiDbContext dbContext, ILogger<GetTopAgentsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<TopAgentDto>> Handle(GetTopAgentsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, MinLimit, MaxLimit);

        _logger.LogInformation("Fetching top {Limit} agents by distinct user count", limit);

        // Aggregate AgentSessionEntity by AgentDefinitionId.
        // Install count = distinct UserId per AgentDefinitionId.
        var aggregated = await _dbContext.AgentSessions.AsNoTracking()
            .GroupBy(s => s.AgentDefinitionId)
            .Select(g => new
            {
                AgentDefinitionId = g.Key,
                InstallCount = g.Select(s => s.UserId).Distinct().Count(),
                LatestActivity = g.Max(s => s.StartedAt)
            })
            .OrderByDescending(x => x.InstallCount)
            .ThenByDescending(x => x.LatestActivity)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return aggregated.Select(x => new TopAgentDto(
            Id: x.AgentDefinitionId,
            Name: string.Empty,
            GameName: string.Empty,
            AgentType: string.Empty,
            InstallCount: x.InstallCount,
            CreatedAt: x.LatestActivity
        )).ToList();
    }
}
