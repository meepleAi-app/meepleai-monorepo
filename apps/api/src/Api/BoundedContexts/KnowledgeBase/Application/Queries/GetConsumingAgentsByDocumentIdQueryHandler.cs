using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetConsumingAgentsByDocumentIdQuery — Issue #1651, F3-FU-2 Used-by tab.
/// Lists agents that explicitly consume the document (KbCardIds containment), resolves
/// GameName via bulk lookup, and maps to KbDocConsumingAgentDto. Soft-deleted agents are
/// excluded by the repository implementation; the global query filter is not relied upon.
/// </summary>
internal sealed class GetConsumingAgentsByDocumentIdQueryHandler
    : IQueryHandler<GetConsumingAgentsByDocumentIdQuery, IReadOnlyList<KbDocConsumingAgentDto>>
{
    private readonly IAgentDefinitionRepository _agentRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public GetConsumingAgentsByDocumentIdQueryHandler(
        IAgentDefinitionRepository agentRepository,
        ISharedGameRepository sharedGameRepository)
    {
        _agentRepository = agentRepository
            ?? throw new ArgumentNullException(nameof(agentRepository));
        _sharedGameRepository = sharedGameRepository
            ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    public async Task<IReadOnlyList<KbDocConsumingAgentDto>> Handle(
        GetConsumingAgentsByDocumentIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var agents = await _agentRepository
            .GetByConsumedDocumentAsync(query.DocumentId, cancellationToken)
            .ConfigureAwait(false);

        if (agents.Count == 0)
            return Array.Empty<KbDocConsumingAgentDto>();

        var gameIds = agents
            .Where(a => a.GameId.HasValue)
            .Select(a => a.GameId!.Value)
            .Distinct()
            .ToList();

        var gameNames = gameIds.Count > 0
            ? await _sharedGameRepository
                .GetNamesByIdsAsync(gameIds, cancellationToken)
                .ConfigureAwait(false)
            : new Dictionary<Guid, string>();

        return agents.Select(a => MapToDto(a, gameNames)).ToList();
    }

    private static KbDocConsumingAgentDto MapToDto(
        Domain.Entities.AgentDefinition agent,
        IReadOnlyDictionary<Guid, string> gameNames)
    {
        var gameName = agent.GameId.HasValue
            && gameNames.TryGetValue(agent.GameId.Value, out var name)
                ? name
                : null;

        return new KbDocConsumingAgentDto(
            Id: agent.Id,
            Name: agent.Name,
            Type: agent.Type.Value,
            IsActive: agent.IsActive,
            Status: agent.Status.ToString(),
            IsSystemDefined: agent.IsSystemDefined,
            TypologySlug: agent.TypologySlug,
            GameId: agent.GameId,
            GameName: gameName,
            InvocationCount: agent.InvocationCount,
            LastInvokedAt: agent.LastInvokedAt);
    }
}
