using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetAllAgentsQuery — returns lightweight AgentDto list for user-facing endpoints.
/// Used by GET /api/v1/games/{id}/agents (chat panel agent resolution).
/// </summary>
internal sealed class GetAllAgentsQueryHandler
    : IRequestHandler<GetAllAgentsQuery, List<AgentDto>>
{
    private readonly IAgentDefinitionRepository _repository;

    public GetAllAgentsQueryHandler(IAgentDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<List<AgentDto>> Handle(
        GetAllAgentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agents = request.ActiveOnly == true
            ? await _repository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false)
            : await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Apply optional Type filter
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            agents = agents
                .Where(a => string.Equals(a.Type.Value, request.Type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Apply optional GameId filter
        if (request.GameId.HasValue)
        {
            agents = agents.Where(a => a.GameId == request.GameId.Value).ToList();
        }

        return agents.Select(MapToDto).ToList();
    }

    private static AgentDto MapToDto(Domain.Entities.AgentDefinition agent)
    {
        var recentThreshold = DateTime.UtcNow.AddHours(-24);
        var idleThreshold = DateTime.UtcNow.AddDays(-7);

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
            CreatedByUserId: null
        );
    }
}
