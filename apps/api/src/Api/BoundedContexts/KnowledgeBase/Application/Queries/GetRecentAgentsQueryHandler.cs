using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetRecentAgentsQuery.
/// Returns agents from both the agents table and user library entries with custom agent config.
/// Issue #4126: API Integration.
/// </summary>
internal sealed class GetRecentAgentsQueryHandler : IQueryHandler<GetRecentAgentsQuery, IReadOnlyList<AgentDto>>
{
    private readonly IAgentRepository _agentRepository;
    private readonly IUserLibraryRepository _libraryRepository;

    public GetRecentAgentsQueryHandler(
        IAgentRepository agentRepository,
        IUserLibraryRepository libraryRepository)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
    }

    public async Task<IReadOnlyList<AgentDto>> Handle(
        GetRecentAgentsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Agents from agents table (user-specific, no LastInvokedAt filter)
        IEnumerable<AgentDto> agentTableDtos;
        if (request.UserId.HasValue)
        {
            var agents = await _agentRepository
                .GetByUserIdAsync(request.UserId.Value, cancellationToken)
                .ConfigureAwait(false);

            agentTableDtos = agents.Select(a => new AgentDto(
                a.Id,
                a.Name,
                a.Type.Value,
                a.Strategy.Name,
                a.Strategy.Parameters ?? new Dictionary<string, object>(StringComparer.Ordinal),
                a.IsActive,
                a.CreatedAt,
                a.LastInvokedAt,
                a.InvocationCount,
                a.IsRecentlyUsed,
                a.IsIdle,
                a.GameId,
                a.CreatedByUserId));
        }
        else
        {
            agentTableDtos = [];
        }

        // 2. Agents configured via UserLibraryEntry.CustomAgentConfigJson
        var libraryDtos = new List<AgentDto>();
        if (request.UserId.HasValue)
        {
            var entries = await _libraryRepository
                .GetUserGamesAsync(request.UserId.Value, null, cancellationToken)
                .ConfigureAwait(false);

            foreach (var entry in entries.Where(e => e.HasCustomAgent()))
            {
                var cfg = entry.CustomAgentConfig!;
                var name = cfg.Personality?.Split(':', 2)[0].Trim() ?? "Custom Agent";
                libraryDtos.Add(new AgentDto(
                    Id: entry.Id,
                    Name: name,
                    Type: "Custom",
                    StrategyName: "Default",
                    StrategyParameters: new Dictionary<string, object>(StringComparer.Ordinal),
                    IsActive: true,
                    CreatedAt: entry.AddedAt,
                    LastInvokedAt: null,
                    InvocationCount: 0,
                    IsRecentlyUsed: false,
                    IsIdle: false,
                    GameId: entry.GameId,
                    CreatedByUserId: entry.UserId));
            }
        }

        // 3. Merge, deduplicate by Id, take Limit
        return agentTableDtos
            .Concat(libraryDtos)
            .DistinctBy(a => a.Id)
            .Take(request.Limit)
            .ToList();
    }
}
