namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// RSVP response status for a game night invitation.
/// Issue #42: GameNightEvent + GameNightRsvp domain entities.
/// </summary>
public enum RsvpStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Maybe = 3
}
