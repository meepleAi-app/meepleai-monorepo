using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetPopularAgents;

/// <summary>
/// Handler for <see cref="GetPopularAgentsQuery"/> — returns the top-N active
/// agents sorted by popularity. Wave 3 Phase 1, PR #732 §4.3.3 / Issue #805.
/// </summary>
/// <remarks>
/// Sort: <c>InstallCount DESC</c>, then <c>InvocationCount DESC</c> (tiebreak),
/// then <c>CreatedAt DESC</c> (final tiebreak for deterministic ordering when
/// both metrics are zero).
///
/// Schema reality v1 carryover: there is no per-user installation tracking
/// entity in the current schema, so <c>InstallCount</c> is always <c>0</c>
/// and the sort effectively collapses to <c>InvocationCount DESC</c>.
/// This is documented as Gate B in the FE Phase 0.5 contract — the wire
/// shape is stable so the discover rail can render today and adopt the real
/// install metric without a fetch shape change.
///
/// Cache TTL: 15 minutes (PR #732 §3.2 caching matrix). Bulk-fetches game
/// names for ordered slice (no N+1) using the same pattern as
/// <see cref="KnowledgeBase.Application.Queries.GetRecentAgentsQueryHandler"/>.
/// </remarks>
internal sealed class GetPopularAgentsQueryHandler
    : IRequestHandler<GetPopularAgentsQuery, IReadOnlyList<PopularAgentDto>>
{
    private const string CacheKey = "discover:popularAgents";
    private const int MaxCacheLimit = 50;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    private readonly IAgentDefinitionRepository _agentRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetPopularAgentsQueryHandler> _logger;

    public GetPopularAgentsQueryHandler(
        IAgentDefinitionRepository agentRepository,
        ISharedGameRepository sharedGameRepository,
        IHybridCacheService cache,
        ILogger<GetPopularAgentsQueryHandler> logger)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<PopularAgentDto>> Handle(
        GetPopularAgentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var allPopular = await _cache.GetOrCreateAsync(
            CacheKey,
            async ct => await ComputePopularAgentsAsync(MaxCacheLimit, ct).ConfigureAwait(false),
            tags: ["agents", "discover", "popularAgents"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        var trimmed = allPopular.Take(request.Limit).ToArray();

        _logger.LogInformation(
            "Returning {Count} popular agents (limit={Limit}) from cache/compute",
            trimmed.Length,
            request.Limit);

        return trimmed;
    }

    private async Task<List<PopularAgentDto>> ComputePopularAgentsAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var agents = await _agentRepository
            .GetAllActiveAsync(cancellationToken)
            .ConfigureAwait(false);

        // Schema reality v1 carryover: InstallCount = 0 (no AgentInstallation entity yet).
        // Sort collapses to InvocationCount DESC + CreatedAt DESC tiebreak in v1.
        var ordered = agents
            .OrderByDescending(a => a.InvocationCount)
            .ThenByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToList();

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

        return ordered.Select(agent =>
        {
            var gameName = agent.GameId.HasValue
                && gameNames.TryGetValue(agent.GameId.Value, out var name)
                    ? name
                    : null;

            return new PopularAgentDto(
                Id: agent.Id,
                Name: agent.Name,
                GameId: agent.GameId,
                GameName: gameName,
                InstallCount: 0,
                InvocationCount: agent.InvocationCount);
        }).ToList();
    }
}
