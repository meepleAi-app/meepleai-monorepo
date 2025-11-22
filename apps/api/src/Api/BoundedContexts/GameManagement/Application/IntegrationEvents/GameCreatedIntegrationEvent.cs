using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Application.IntegrationEvents;

/// <summary>
/// Integration event published when a game is created.
/// Subscribers: WorkflowIntegration (trigger game setup workflows), KnowledgeBase (prepare vector indices)
/// </summary>
public sealed class GameCreatedIntegrationEvent : IntegrationEventBase
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
