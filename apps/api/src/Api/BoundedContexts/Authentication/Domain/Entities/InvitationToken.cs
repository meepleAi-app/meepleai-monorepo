using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Aggregate root representing an admin-issued invitation token.
/// Tracks invitation lifecycle: creation, acceptance, and expiration.
/// </summary>
internal sealed class InvitationToken : AggregateRoot<Guid>
{
    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string TokenHash { get; private set; } = string.Empty;
    public Guid InvitedByUserId { get; private set; }
    public InvitationStatus Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? CustomMessage { get; private set; }
    public Guid? PendingUserId { get; private set; }

    private readonly List<InvitationGameSuggestion> _gameSuggestions = new();
    public IReadOnlyList<InvitationGameSuggestion> GameSuggestions => _gameSuggestions.AsReadOnly();

    private static readonly string[] AllowedRoles = { "User", "Editor", "Admin" };

    // EF Core constructor
    private InvitationToken() : base() { }
    private InvitationToken(Guid id) : base(id) { }

    /// <summary>
    /// Internal constructor for repository materialization (avoids reflection).
    /// </summary>
    internal static InvitationToken CreateForHydration(Guid id) => new(id);

    /// <summary>
    /// Factory method to create a new invitation token.
    /// </summary>
    /// <param name="email">Email address of the invitee</param>
    /// <param name="role">Role to assign upon acceptance</param>
    /// <param name="tokenHash">SHA-256 hash of the invitation token</param>
    /// <param name="invitedByUserId">Admin user who created the invitation</param>
    /// <returns>New InvitationToken instance</returns>
    public static InvitationToken Create(
        string email, string role, string tokenHash, Guid invitedByUserId)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty", nameof(email));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));
        if (!AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Role '{role}' is not allowed for invitation", nameof(role));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash cannot be empty", nameof(tokenHash));
        if (invitedByUserId == Guid.Empty)
            throw new ArgumentException("Invited by user ID cannot be empty", nameof(invitedByUserId));

        return new InvitationToken(Guid.NewGuid())
        {
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            TokenHash = tokenHash,
            InvitedByUserId = invitedByUserId,
            Status = InvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method to create a new invitation token with extended provisioning data.
    /// Uses TimeProvider for testable time handling.
    /// </summary>
    /// <param name="email">Email address value object of the invitee</param>
    /// <param name="role">Role value object to assign upon acceptance</param>
    /// <param name="pendingUserId">ID of the pre-provisioned pending user</param>
    /// <param name="customMessage">Optional custom message from admin to invitee</param>
    /// <param name="expiresInDays">Number of days until the invitation expires</param>
    /// <param name="timeProvider">TimeProvider for testable time handling</param>
    /// <param name="invitedByUserId">Admin user who created the invitation</param>
    /// <param name="tokenHash">SHA-256 hash of the invitation token</param>
    /// <returns>New InvitationToken instance with extended data</returns>
    public static InvitationToken Create(
        Email email,
        Role role,
        Guid pendingUserId,
        string? customMessage,
        int expiresInDays,
        TimeProvider timeProvider,
        string tokenHash,
        Guid invitedByUserId = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (pendingUserId == Guid.Empty)
            throw new ArgumentException("Pending user ID cannot be empty", nameof(pendingUserId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash cannot be empty", nameof(tokenHash));

        var now = timeProvider.GetUtcNow().UtcDateTime;

        return new InvitationToken(Guid.NewGuid())
        {
            Email = email.Value,
            Role = role.Value,
            TokenHash = tokenHash,
            InvitedByUserId = invitedByUserId,
            Status = InvitationStatus.Pending,
            ExpiresAt = now.AddDays(expiresInDays),
            CreatedAt = now,
            CustomMessage = customMessage,
            PendingUserId = pendingUserId
        };
    }

    /// <summary>
    /// Adds a game suggestion to this invitation token.
    /// </summary>
    /// <param name="gameId">The game to suggest or pre-add</param>
    /// <param name="type">Whether the game is pre-added or merely suggested</param>
    public void AddGameSuggestion(Guid gameId, GameSuggestionType type)
    {
        _gameSuggestions.Add(InvitationGameSuggestion.Create(Id, gameId, type));
    }

    /// <summary>
    /// Marks the invitation as accepted by a user.
    /// </summary>
    /// <param name="acceptedByUserId">The user who accepted the invitation</param>
    public void MarkAccepted(Guid acceptedByUserId)
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be accepted");
        if (DateTime.UtcNow > ExpiresAt)
            throw new InvalidOperationException("Invitation has expired");
        if (acceptedByUserId == Guid.Empty)
            throw new ArgumentException("Accepted by user ID cannot be empty", nameof(acceptedByUserId));

        Status = InvitationStatus.Accepted;
        AcceptedAt = DateTime.UtcNow;
        AcceptedByUserId = acceptedByUserId;
    }

    /// <summary>
    /// Marks the invitation as expired.
    /// </summary>
    public void MarkExpired()
    {
        if (Status == InvitationStatus.Accepted || Status == InvitationStatus.Revoked)
            throw new InvalidOperationException("Cannot expire an accepted or revoked invitation");
        Status = InvitationStatus.Expired;
    }

    /// <summary>
    /// Revokes a pending invitation, making it immediately invalid.
    /// </summary>
    public void Revoke()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be revoked");

        Status = InvitationStatus.Revoked;
        RevokedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if the invitation is still valid (pending and not expired).
    /// </summary>
    public bool IsValid => Status == InvitationStatus.Pending && DateTime.UtcNow <= ExpiresAt;

    #region Persistence Hydration Methods (internal - S3011 fix)

    /// <summary>
    /// Restores invitation state from persistence layer.
    /// Internal method to avoid reflection in repository (S3011 compliance).
    /// Should only be called by InvitationTokenRepository during entity materialization.
    /// </summary>
    internal void RestoreState(
        string email,
        string role,
        string tokenHash,
        Guid invitedByUserId,
        InvitationStatus status,
        DateTime expiresAt,
        DateTime? acceptedAt,
        Guid? acceptedByUserId,
        DateTime createdAt,
        DateTime? revokedAt = null,
        string? customMessage = null,
        Guid? pendingUserId = null)
    {
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        Status = status;
        ExpiresAt = expiresAt;
        AcceptedAt = acceptedAt;
        AcceptedByUserId = acceptedByUserId;
        CreatedAt = createdAt;
        RevokedAt = revokedAt;
        CustomMessage = customMessage;
        PendingUserId = pendingUserId;
    }

    #endregion
}
