using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for <see cref="GetRecentAgentsQuery"/> — returns the top-N active <see cref="AgentDto"/>
/// list ordered by <c>LastInvokedAt</c> descending. Powers the dashboard recent-agents widget
/// (frontend <c>useRecentAgents</c>). Issue #650 (Phase γ.3).
/// </summary>
/// <remarks>
/// Mirrors the GameName drift-fix lookup pattern from <see cref="GetAllAgentsQueryHandler"/>
/// and <see cref="GetAgentByIdQueryHandler"/> (PR #662). The <c>UserId</c> field on the query is
/// reserved for a future user-owned-agent feature; current schema scopes agents per game (or
/// globally), not per user, so it is intentionally unused at this time.
/// </remarks>
internal sealed class GetRecentAgentsQueryHandler
    : IRequestHandler<GetRecentAgentsQuery, IReadOnlyList<AgentDto>>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public GetRecentAgentsQueryHandler(
        IAgentDefinitionRepository repository,
        ISharedGameRepository sharedGameRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    public async Task<IReadOnlyList<AgentDto>> Handle(
        GetRecentAgentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Defensive clamp: dashboard widget caller passes default 10; protect against runaway
        // payloads when client supplies large limit values.
        var safeLimit = Math.Clamp(request.Limit, 1, 50);

        var agents = await _repository
            .GetAllActiveAsync(cancellationToken)
            .ConfigureAwait(false);

        var ordered = agents
            .OrderByDescending(a => a.LastInvokedAt ?? DateTime.MinValue)
            .ThenByDescending(a => a.CreatedAt)
            .Take(safeLimit)
            .ToList();

        // Bulk-fetch GameName for ordered slice only (single SQL call, no N+1) — mirrors PR #662.
        var gameIds = ordered
            .Where(a => a.GameId.HasValue)
            .Select(a => a.GameId!.Value)
            .Distinct()
            .ToList();

        var gameNames = gameIds.Count > 0
            ? await _sharedGameRepository
                .GetNamesByIdsAsync(gameIds, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<Guid, string>();

        var recentThreshold = DateTime.UtcNow.AddHours(-24);
        var idleThreshold = DateTime.UtcNow.AddDays(-7);

        return ordered.Select(agent =>
        {
            var gameName = agent.GameId.HasValue
                && gameNames.TryGetValue(agent.GameId.Value, out var name)
                    ? name
                    : null;

            return new AgentDto(
                Id: agent.Id,
                Name: agent.Name,
                Type: agent.Type.Value,
                StrategyName: agent.Strategy.Name,
                StrategyParameters: agent.Strategy.Parameters,
                IsActive: agent.IsActive,
                CreatedAt: agent.CreatedAt,
                LastInvokedAt: agent.LastInvokedAt,
                InvocationCount: agent.InvocationCount,
                IsRecentlyUsed: agent.LastInvokedAt.HasValue && agent.LastInvokedAt.Value > recentThreshold,
                IsIdle: !agent.LastInvokedAt.HasValue || agent.LastInvokedAt.Value < idleThreshold,
                GameId: agent.GameId,
                GameName: gameName,
                CreatedByUserId: null);
        }).ToList();
    }
}
