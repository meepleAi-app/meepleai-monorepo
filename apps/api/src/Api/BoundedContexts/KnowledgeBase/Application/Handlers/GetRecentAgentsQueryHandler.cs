using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using System.Linq;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GetRecentAgentsQuery.
/// Returns agents ordered by last invocation time.
/// Issue #4126: API Integration.
/// </summary>
internal sealed class GetRecentAgentsQueryHandler : IQueryHandler<GetRecentAgentsQuery, IReadOnlyList<AgentDto>>
{
    private readonly IAgentRepository _agentRepository;

    public GetRecentAgentsQueryHandler(IAgentRepository agentRepository)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
    }

    public async Task<IReadOnlyList<AgentDto>> Handle(
        GetRecentAgentsQuery request,
        CancellationToken cancellationToken)
    {
        // Get all agents ordered by LastInvokedAt DESC
        var agents = await _agentRepository
            .GetAllAsync(cancellationToken)
            .ConfigureAwait(false);

        var recent = agents
            .Where(a => a.LastInvokedAt.HasValue)
            .OrderByDescending(a => a.LastInvokedAt)
            .Take(request.Limit)
            .Select(a => new AgentDto(
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
                a.CreatedByUserId
            ))
            .ToList();

        return recent;
    }
}
