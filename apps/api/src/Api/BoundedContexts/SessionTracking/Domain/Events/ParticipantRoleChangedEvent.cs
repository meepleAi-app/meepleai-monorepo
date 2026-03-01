using Api.BoundedContexts.SessionTracking.Domain.Enums;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Event raised when a participant's role is changed.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
public record ParticipantRoleChangedEvent : INotification
{
    public Guid SessionId { get; init; }
    public Guid ParticipantId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public ParticipantRole PreviousRole { get; init; }
    public ParticipantRole NewRole { get; init; }
    public Guid ChangedBy { get; init; }
    public DateTime Timestamp { get; init; }
}
