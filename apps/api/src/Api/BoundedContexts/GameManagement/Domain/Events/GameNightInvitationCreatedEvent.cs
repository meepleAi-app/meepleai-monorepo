using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a token-based game-night invitation is created.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// Triggers downstream side effects: email dispatch via
/// <see cref="Api.BoundedContexts.UserNotifications"/> integration handlers,
/// and any analytics / activity log entries.
/// </remarks>
internal sealed class GameNightInvitationCreatedEvent : DomainEventBase
{
    public Guid GameNightInvitationId { get; }
    public Guid GameNightId { get; }
    public string Email { get; }
    public string Token { get; }
    public DateTimeOffset ExpiresAt { get; }

    public GameNightInvitationCreatedEvent(
        Guid gameNightInvitationId,
        Guid gameNightId,
        string email,
        string token,
        DateTimeOffset expiresAt)
    {
        GameNightInvitationId = gameNightInvitationId;
        GameNightId = gameNightId;
        Email = email;
        Token = token;
        ExpiresAt = expiresAt;
    }
}
