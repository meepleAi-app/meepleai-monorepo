using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Event raised when a new game session state is initialized.
/// </summary>
internal sealed class GameStateInitializedEvent : DomainEventBase
{
    public Guid StateId { get; }
    public Guid GameSessionId { get; }
    public Guid TemplateId { get; }

    public GameStateInitializedEvent(Guid stateId, Guid gameSessionId, Guid templateId)
    {
        StateId = stateId;
        GameSessionId = gameSessionId;
        TemplateId = templateId;
    }
}
