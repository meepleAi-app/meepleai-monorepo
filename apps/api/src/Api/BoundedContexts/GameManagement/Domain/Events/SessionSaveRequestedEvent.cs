using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a session save is requested (auto-save, pause snapshot, resume prep).
/// </summary>
internal sealed class SessionSaveRequestedEvent : DomainEventBase
{
    public Guid PauseSnapshotId { get; }
    public Guid LiveGameSessionId { get; }
    public Guid AgentDefinitionId { get; }
    public List<string> LastMessages { get; }

    public SessionSaveRequestedEvent(
        Guid pauseSnapshotId,
        Guid liveGameSessionId,
        Guid agentDefinitionId,
        List<string> lastMessages)
    {
        PauseSnapshotId = pauseSnapshotId;
        LiveGameSessionId = liveGameSessionId;
        AgentDefinitionId = agentDefinitionId;
        LastMessages = lastMessages;
    }
}
