using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for <see cref="GetAgentByIdQuery"/> — returns a single <see cref="AgentDto"/> for the
/// user-facing <c>GET /api/v1/agents/{id}</c> route. Issue #647 (Phase γ.1).
/// </summary>
/// <remarks>
/// Mirrors the mapping in <see cref="GetAllAgentsQueryHandler"/>, including the GameName drift-fix
/// lookup against <see cref="ISharedGameRepository.GetNamesByIdsAsync"/> introduced in PR #662.
/// Returns <c>null</c> when no agent matches the supplied id (route surfaces 404).
/// </remarks>
internal sealed class GetAgentByIdQueryHandler
    : IRequestHandler<GetAgentByIdQuery, AgentDto?>
{
    private readonly IAgentDefinitionRepository _repository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public GetAgentByIdQueryHandler(
        IAgentDefinitionRepository repository,
        ISharedGameRepository sharedGameRepository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    public async Task<AgentDto?> Handle(
        GetAgentByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var agent = await _repository
            .GetByIdAsync(request.AgentId, cancellationToken)
            .ConfigureAwait(false);

        if (agent is null)
        {
            return null;
        }

        // GameName drift-fix lookup (PR #662 pattern): single bulk call even for one id keeps
        // the resolution path identical to GetAllAgentsQueryHandler.
        string? gameName = null;
        if (agent.GameId.HasValue)
        {
            var names = await _sharedGameRepository
                .GetNamesByIdsAsync(new[] { agent.GameId.Value }, cancellationToken)
                .ConfigureAwait(false);
            names.TryGetValue(agent.GameId.Value, out gameName);
        }

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
            GameName: gameName,
            CreatedByUserId: null);
    }
}
