using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetAllAgentsQuery — returns lightweight AgentDto list for user-facing endpoints.
/// Used by GET /api/v1/games/{id}/agents (chat panel agent resolution) and GET /agents.
/// </summary>
/// <remarks>
/// Issue #660: AgentDto.GameName populated via single bulk lookup against SharedGame catalog
/// (avoids N+1 queries when listing all agents).
/// Issue #1589 (BE-2): scope=my-library filters to agents whose GameId is in the caller's
/// library (via IUserLibraryRepository) plus system agents (GameId == null).
/// </remarks>
internal sealed class GetAllAgentsQueryHandler
    : IRequestHandler<GetAllAgentsQuery, List<AgentDto>>
{
    private const string MyLibraryScope = "my-library";

    private readonly IAgentDefinitionRepository _repository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUserLibraryRepository _userLibraryRepository;

    public GetAllAgentsQueryHandler(
        IAgentDefinitionRepository repository,
        ISharedGameRepository sharedGameRepository,
        IUserLibraryRepository userLibraryRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _userLibraryRepository = userLibraryRepository
            ?? throw new ArgumentNullException(nameof(userLibraryRepository));
    }

    public async Task<List<AgentDto>> Handle(
        GetAllAgentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agents = request.ActiveOnly == true
            ? await _repository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false)
            : await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // BE-2 #1589: scope=my-library — agents in the caller's library games + system agents.
        if (string.Equals(request.Scope, MyLibraryScope, StringComparison.Ordinal)
            && request.ScopeUserId.HasValue)
        {
            var libraryGames = await _userLibraryRepository
                .GetUserGamesAsync(request.ScopeUserId.Value, null, cancellationToken)
                .ConfigureAwait(false);

            var libraryGameIds = libraryGames
                .Select(e => e.GameId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToHashSet();

            // 🔴 #4081: la condizione era `!a.GameId.HasValue`, cioe' «qualunque agente senza
            // gioco», mentre il commento sopra promette «agenti di sistema». Oggi i due insiemi
            // coincidono perche' l'unico agente esistente e' entrambe le cose, ma un agente senza
            // gioco creato per via amministrativa e NON di sistema comparirebbe nella libreria
            // personale di ogni utente. Non e' raggiungibile da un utente (`CreateUserAgentCommand`
            // e `CreateGameAgentCommand` prendono un `Guid GameId` non nullable), quindi e' una
            // divergenza latente e non una fuga viva — ma il codice deve dire cio' che il commento
            // promette.
            agents = agents
                .Where(a => a.IsSystemDefined || (a.GameId.HasValue && libraryGameIds.Contains(a.GameId.Value)))
                .ToList();
        }

        // Issue #4138: an optional Type filter stood here. AgentDefinition has no type
        // any more, so there is nothing to filter on.

        // Apply optional GameId filter
        if (request.GameId.HasValue)
        {
            agents = agents.Where(a => a.GameId == request.GameId.Value).ToList();
        }

        // Issue #660: Bulk-fetch game names (single query, no N+1)
        var gameIds = agents
            .Where(a => a.GameId.HasValue)
            .Select(a => a.GameId!.Value)
            .Distinct()
            .ToList();

        var gameNames = gameIds.Count > 0
            ? await _sharedGameRepository.GetNamesByIdsAsync(gameIds, cancellationToken).ConfigureAwait(false)
            : new Dictionary<Guid, string>();

        return agents.Select(a => MapToDto(a, gameNames)).ToList();
    }

    private static AgentDto MapToDto(
        Domain.Entities.AgentDefinition agent,
        IReadOnlyDictionary<Guid, string> gameNames)
    {
        var recentThreshold = DateTime.UtcNow.AddHours(-24);
        var idleThreshold = DateTime.UtcNow.AddDays(-7);

        var gameName = agent.GameId.HasValue
            && gameNames.TryGetValue(agent.GameId.Value, out var name)
                ? name
                : null;

        return new AgentDto(
            Id: agent.Id,
            Name: agent.Name,
            StrategyName: agent.Strategy.Name,
            StrategyParameters: agent.Strategy.Parameters,
            IsActive: agent.IsActive,
            CreatedAt: agent.CreatedAt,
            LastInvokedAt: agent.LastInvokedAt,
            InvocationCount: agent.InvocationCount,
            IsRecentlyUsed: agent.LastInvokedAt.HasValue && agent.LastInvokedAt.Value > recentThreshold,
            IsIdle: !agent.LastInvokedAt.HasValue || agent.LastInvokedAt.Value < idleThreshold,
            IsSystemDefined: agent.IsSystemDefined,
            GameId: agent.GameId,
            GameName: gameName,
            CreatedByUserId: null
        );
    }
}
