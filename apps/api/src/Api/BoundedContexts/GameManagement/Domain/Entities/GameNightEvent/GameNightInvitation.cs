using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;

/// <summary>
/// Aggregate root representing a token-based game-night invitation.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
/// <remarks>
/// <para>
/// Distinct from the existing <see cref="GameNightRsvp"/> entity owned by
/// <see cref="GameNightEvent"/>: that flow tracks RSVPs by <c>UserId</c> for users already
/// known to the system. This aggregate persists the public, token-addressable invitation
/// (sent by email to addresses that may not yet be registered) and its lifecycle.
/// </para>
/// <para>
/// Idempotency contract (D2 b — see spec §2.2): repeating the current response is a
/// no-op (returns <c>false</c>); attempting to switch between Accepted ⇄ Declined is
/// rejected and surfaces as 409 Conflict at the endpoint layer; responses on terminal
/// Expired/Cancelled invitations are rejected and surface as 410 Gone.
/// </para>
/// </remarks>
internal sealed class GameNightInvitation : AggregateRoot<Guid>
{
    public string Token { get; private set; }
    public Guid GameNightId { get; private set; }
    public string Email { get; private set; }
    public GameNightInvitationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RespondedAt { get; private set; }
    public Guid? RespondedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

#pragma warning disable CS8618
    private GameNightInvitation() : base() { } // EF Core
#pragma warning restore CS8618

    private GameNightInvitation(
        Guid id,
        string token,
        Guid gameNightId,
        string email,
        GameNightInvitationStatus status,
        DateTimeOffset expiresAt,
        DateTimeOffset? respondedAt,
        Guid? respondedByUserId,
        DateTimeOffset createdAt,
        Guid createdBy) : base(id)
    {
        Token = token;
        GameNightId = gameNightId;
        Email = email;
        Status = status;
        ExpiresAt = expiresAt;
        RespondedAt = respondedAt;
        RespondedByUserId = respondedByUserId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    /// <summary>
    /// Creates a new pending invitation with a freshly-generated token.
    /// Raises <see cref="GameNightInvitationCreatedEvent"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="gameNightId"/> or <paramref name="createdBy"/> is empty,
    /// when <paramref name="email"/> is null/whitespace, or when
    /// <paramref name="expiresAt"/> is not strictly in the future relative to
    /// <paramref name="utcNow"/>.
    /// </exception>
    public static GameNightInvitation Create(
        Guid gameNightId,
        string email,
        DateTimeOffset expiresAt,
        Guid createdBy,
        DateTimeOffset utcNow)
    {
        if (gameNightId == Guid.Empty)
            throw new ArgumentException("GameNightId is required", nameof(gameNightId));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("CreatedBy is required", nameof(createdBy));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
        if (expiresAt <= utcNow)
            throw new ArgumentException("ExpiresAt must be in the future", nameof(expiresAt));

        var token = InvitationToken.Generate().Value;
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var id = Guid.NewGuid();

        var invitation = new GameNightInvitation(
            id: id,
            token: token,
            gameNightId: gameNightId,
            email: normalizedEmail,
            status: GameNightInvitationStatus.Pending,
            expiresAt: expiresAt,
            respondedAt: null,
            respondedByUserId: null,
            createdAt: utcNow,
            createdBy: createdBy);

        invitation.AddDomainEvent(new GameNightInvitationCreatedEvent(
            gameNightInvitationId: id,
            gameNightId: gameNightId,
            email: normalizedEmail,
            token: token,
            expiresAt: expiresAt));

        return invitation;
    }

    /// <summary>
    /// Reconstitutes an invitation from persistence data. Skips invariant checks.
    /// </summary>
    internal static GameNightInvitation Reconstitute(
        Guid id,
        string token,
        Guid gameNightId,
        string email,
        GameNightInvitationStatus status,
        DateTimeOffset expiresAt,
        DateTimeOffset? respondedAt,
        Guid? respondedByUserId,
        DateTimeOffset createdAt,
        Guid createdBy)
    {
        return new GameNightInvitation(
            id: id,
            token: token,
            gameNightId: gameNightId,
            email: email,
            status: status,
            expiresAt: expiresAt,
            respondedAt: respondedAt,
            respondedByUserId: respondedByUserId,
            createdAt: createdAt,
            createdBy: createdBy);
    }

    /// <summary>
    /// Accepts the invitation. Idempotent (D2 b):
    /// Pending→Accepted returns <c>true</c>; Accepted→Accepted no-ops returns <c>false</c>;
    /// Declined throws (caller maps to 409); Expired/Cancelled throws (caller maps to 410).
    /// </summary>
    public bool Accept(Guid? userId, DateTimeOffset utcNow)
    {
        return Respond(GameNightInvitationStatus.Accepted, userId, utcNow);
    }

    /// <summary>
    /// Declines the invitation. Idempotent (D2 b) — symmetric to <see cref="Accept"/>.
    /// </summary>
    public bool Decline(Guid? userId, DateTimeOffset utcNow)
    {
        return Respond(GameNightInvitationStatus.Declined, userId, utcNow);
    }

    /// <summary>
    /// Cancels a pending or accepted invitation (organizer revokes, or parent GameNight
    /// is cancelled). Idempotent: cancelling an already-cancelled invitation is a no-op.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// When the invitation is in <see cref="GameNightInvitationStatus.Declined"/> or
    /// <see cref="GameNightInvitationStatus.Expired"/> (no transition path defined).
    /// </exception>
    public void Cancel(DateTimeOffset utcNow)
    {
        switch (Status)
        {
            case GameNightInvitationStatus.Cancelled:
                return; // idempotent
            case GameNightInvitationStatus.Pending:
            case GameNightInvitationStatus.Accepted:
                Status = GameNightInvitationStatus.Cancelled;
                RespondedAt = utcNow;
                return;
            case GameNightInvitationStatus.Declined:
            case GameNightInvitationStatus.Expired:
                throw new InvalidOperationException(
                    $"Cannot cancel invitation in status {Status}.");
            default:
                throw new InvalidOperationException(
                    $"Unknown invitation status: {Status}.");
        }
    }

    /// <summary>
    /// True iff the invitation is still <see cref="GameNightInvitationStatus.Pending"/>
    /// and the cutoff has passed. Terminal states (Accepted/Declined/Cancelled) are not
    /// considered expired — expiry only matters for un-responded invitations.
    /// </summary>
    public bool IsExpired(DateTimeOffset utcNow)
    {
        return Status == GameNightInvitationStatus.Pending && utcNow >= ExpiresAt;
    }

    private bool Respond(
        GameNightInvitationStatus desired,
        Guid? userId,
        DateTimeOffset utcNow)
    {
        // Terminal lifecycle states block all responses (caller maps to 410).
        if (Status == GameNightInvitationStatus.Cancelled ||
            Status == GameNightInvitationStatus.Expired)
        {
            throw new InvalidOperationException(
                $"Cannot respond to invitation in status {Status}.");
        }

        // Implicit expiry on read: pending invitation past cutoff is treated as expired.
        if (Status == GameNightInvitationStatus.Pending && utcNow >= ExpiresAt)
        {
            throw new InvalidOperationException(
                "Cannot respond to invitation: it has expired.");
        }

        // Idempotent same-response (D2 b): no transition, no event.
        if (Status == desired)
        {
            return false;
        }

        // Switching between Accepted ⇄ Declined is rejected (caller maps to 409).
        if (Status != GameNightInvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot change response: invitation is already {Status}.");
        }

        Status = desired;
        RespondedAt = utcNow;
        RespondedByUserId = userId;

        AddDomainEvent(new GameNightInvitationRespondedEvent(
            gameNightInvitationId: Id,
            gameNightId: GameNightId,
            token: Token,
            status: desired,
            respondedByUserId: userId));

        return true;
    }
}
