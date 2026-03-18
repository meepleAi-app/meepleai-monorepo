using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Events;

/// <summary>
/// Domain event raised when an agent is automatically created from a PDF upload.
/// </summary>
internal sealed class AgentAutoCreatedEvent : DomainEventBase
{
    public Guid AgentDefinitionId { get; }
    public Guid PrivateGameId { get; }
    public Guid UserId { get; }
    public string GameName { get; }

    public AgentAutoCreatedEvent(
        Guid agentDefinitionId,
        Guid privateGameId,
        Guid userId,
        string gameName)
    {
        AgentDefinitionId = agentDefinitionId;
        PrivateGameId = privateGameId;
        UserId = userId;
        GameName = gameName;
    }
}
