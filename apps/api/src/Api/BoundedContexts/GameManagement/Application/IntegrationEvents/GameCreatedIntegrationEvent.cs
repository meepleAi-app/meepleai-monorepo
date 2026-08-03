using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Application.IntegrationEvents;

/// <summary>
/// Integration event published when a game is created.
/// Currently has no subscribers: the WorkflowIntegration subscriber was removed with the n8n
/// decommission, and a KnowledgeBase index-prep subscriber was documented but never implemented.
/// Still published (a MediatR publish with no handlers is a harmless no-op) for future
/// cross-context consumers; the dead publish + this event are a candidate for a follow-up cleanup.
/// </summary>
internal sealed class GameCreatedIntegrationEvent : IntegrationEventBase
{
    public Guid GameId { get; }
    public string GameName { get; }
    public int? BggId { get; }

    public GameCreatedIntegrationEvent(Guid gameId, string gameName, int? bggId = null)
        : base("GameManagement")
    {
        GameId = gameId;
        GameName = gameName;
        BggId = bggId;
    }
}
