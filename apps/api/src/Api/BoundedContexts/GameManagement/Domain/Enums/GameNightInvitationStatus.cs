namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Lifecycle status of a token-based game-night invitation.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
public enum GameNightInvitationStatus
{
    /// <summary>Invitation created, awaiting guest response.</summary>
    Pending = 0,

    /// <summary>Guest accepted the invitation.</summary>
    Accepted = 1,

    /// <summary>Guest declined the invitation.</summary>
    Declined = 2,

    /// <summary>Expiration cutoff passed without a response.</summary>
    Expired = 3,

    /// <summary>Cancelled by organizer (or because the parent game night was cancelled).</summary>
    Cancelled = 4,
}
