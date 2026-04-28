using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

/// <summary>
/// Domain event raised when a guest responds (Accept/Decline) to a token-based
/// game-night invitation.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// Carries <see cref="RespondedByUserId"/> only when the responder happened to be
/// authenticated at response time (optional cookie auth on the public endpoint).
/// Anonymous guests yield <c>null</c>.
/// </remarks>
internal sealed class GameNightInvitationRespondedEvent : DomainEventBase
{
    public Guid GameNightInvitationId { get; }
    public Guid GameNightId { get; }
    public string Token { get; }
    public GameNightInvitationStatus Status { get; }
    public Guid? RespondedByUserId { get; }

    public GameNightInvitationRespondedEvent(
        Guid gameNightInvitationId,
        Guid gameNightId,
        string token,
        GameNightInvitationStatus status,
        Guid? respondedByUserId)
    {
        GameNightInvitationId = gameNightInvitationId;
        GameNightId = gameNightId;
        Token = token;
        Status = status;
        RespondedByUserId = respondedByUserId;
    }
}
