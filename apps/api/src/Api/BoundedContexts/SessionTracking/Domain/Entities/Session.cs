using System.ComponentModel.DataAnnotations;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Session aggregate root representing a collaborative game session.
/// </summary>
public class Session
{
    private readonly List<Participant> _participants = [];

    /// <summary>
    /// Session unique identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// User who created the session (owner).
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Game reference (nullable for generic sessions).
    /// </summary>
    public Guid? GameId { get; private set; }

    /// <summary>
    /// Unique 6-character session code for easy sharing.
    /// </summary>
    [MaxLength(6)]
    public string SessionCode { get; private set; } = string.Empty;

    /// <summary>
    /// Session type: Generic or GameSpecific.
    /// </summary>
    public SessionType SessionType { get; private set; }

    /// <summary>
    /// Current session status.
    /// </summary>
    public SessionStatus Status { get; private set; }

    /// <summary>
    /// Date and time when the session was played.
    /// </summary>
    public DateTime SessionDate { get; private set; }

    /// <summary>
    /// Optional location where session was played.
    /// </summary>
    [MaxLength(100)]
    public string? Location { get; private set; }

    /// <summary>
    /// When the session was finalized.
    /// </summary>
    public DateTime? FinalizedAt { get; private set; }

    /// <summary>
    /// Soft delete flag.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Soft delete timestamp.
    /// </summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// Unique invite token for session sharing.
    /// </summary>
    [MaxLength(64)]
    public string? InviteToken { get; private set; }

    /// <summary>
    /// When the invite token expires (null = never expires).
    /// </summary>
    public DateTime? InviteExpiresAt { get; private set; }

    /// <summary>
    /// Audit: Created timestamp.
    /// </summary>
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Audit: Last updated timestamp.
    /// </summary>
    public DateTime? UpdatedAt { get; private set; }

    /// <summary>
    /// Audit: Creator user ID.
    /// </summary>
    public Guid CreatedBy { get; private set; }

    /// <summary>
    /// Audit: Last updater user ID.
    /// </summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>
    /// Optimistic concurrency token.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; private set; }

    /// <summary>
    /// Session participants.
    /// </summary>
    public IReadOnlyCollection<Participant> Participants => _participants.AsReadOnly();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Session()
    {
    }

    /// <summary>
    /// Factory method to create a new session.
    /// </summary>
    /// <param name="userId">User creating the session.</param>
    /// <param name="gameId">Optional game reference.</param>
    /// <param name="sessionType">Session type.</param>
    /// <param name="location">Optional location.</param>
    /// <param name="sessionDate">Optional session date (defaults to now).</param>
    /// <returns>New session instance.</returns>
    public static Session Create(
        Guid userId,
        Guid? gameId,
        SessionType sessionType,
        string? location = null,
        DateTime? sessionDate = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (sessionType == SessionType.GameSpecific && gameId == null)
            throw new ArgumentException("Game ID required for GameSpecific sessions.", nameof(gameId));

        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            SessionCode = GenerateSessionCode(),
            SessionType = sessionType,
            Status = SessionStatus.Active,
            SessionDate = sessionDate ?? DateTime.UtcNow,
            Location = location,
            CreatedBy = userId,
            IsDeleted = false
        };

        // Automatically add creator as first participant (owner)
        session.AddParticipant(
            ParticipantInfo.Create(displayName: "Owner", isOwner: true, joinOrder: 1),
            userId
        );

        return session;
    }

    /// <summary>
    /// Adds a participant to the session.
    /// </summary>
    /// <param name="participantInfo">Participant information.</param>
    /// <param name="userId">Optional user ID for registered users.</param>
    public void AddParticipant(ParticipantInfo participantInfo, Guid? userId = null)
    {
        if (Status == SessionStatus.Finalized)
            throw new ConflictException("Cannot add participants to finalized session.");

        ArgumentNullException.ThrowIfNull(participantInfo);

        var participant = new Participant
        {
            Id = Guid.NewGuid(),
            SessionId = Id,
            UserId = userId,
            DisplayName = participantInfo.DisplayName,
            IsOwner = participantInfo.IsOwner,
            JoinOrder = _participants.Count + 1,
            CreatedAt = DateTime.UtcNow
        };

        _participants.Add(participant);
    }

    /// <summary>
    /// Pauses an active session.
    /// </summary>
    public void Pause()
    {
        if (Status != SessionStatus.Active)
            throw new ConflictException($"Cannot pause session with status {Status}.");

        Status = SessionStatus.Paused;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resumes a paused session.
    /// </summary>
    public void Resume()
    {
        if (Status != SessionStatus.Paused)
            throw new ConflictException($"Cannot resume session with status {Status}.");

        Status = SessionStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Finalizes the session (no further modifications allowed).
    /// </summary>
    /// <param name="winnerId">Optional winner participant ID.</param>
    public void Finalize(Guid? winnerId = null)
    {
        if (Status == SessionStatus.Finalized)
            throw new ConflictException("Session is already finalized.");

        if (winnerId.HasValue && !_participants.Any(p => p.Id == winnerId.Value))
            throw new ArgumentException("Winner ID must be a valid participant.", nameof(winnerId));

        Status = SessionStatus.Finalized;
        FinalizedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        // If winner specified, mark as rank 1
        if (winnerId.HasValue)
        {
            var winner = _participants.First(p => p.Id == winnerId.Value);
            winner.SetFinalRank(1);
        }
    }

    /// <summary>
    /// Updates audit information.
    /// </summary>
    /// <param name="userId">User performing the update.</param>
    public void UpdateAudit(Guid userId)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = userId;
    }

    /// <summary>
    /// Soft deletes the session.
    /// </summary>
    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Generates an invite token for the session.
    /// </summary>
    /// <param name="expiresInHours">Optional expiration in hours (null = never expires).</param>
    /// <returns>The generated invite token.</returns>
    public string GenerateInviteToken(int? expiresInHours = null)
    {
        if (Status == SessionStatus.Finalized)
            throw new ConflictException("Cannot generate invite for finalized session.");

        InviteToken = GenerateSecureToken();
        InviteExpiresAt = expiresInHours.HasValue
            ? DateTime.UtcNow.AddHours(expiresInHours.Value)
            : null;
        UpdatedAt = DateTime.UtcNow;

        return InviteToken;
    }

    /// <summary>
    /// Validates if the invite token is valid and not expired.
    /// </summary>
    /// <param name="token">Token to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsInviteTokenValid(string token)
    {
        if (string.IsNullOrEmpty(InviteToken))
            return false;

        if (!string.Equals(InviteToken, token, StringComparison.Ordinal))
            return false;

        if (InviteExpiresAt.HasValue && InviteExpiresAt.Value < DateTime.UtcNow)
            return false;

        return true;
    }

    /// <summary>
    /// Revokes the current invite token.
    /// </summary>
    public void RevokeInviteToken()
    {
        InviteToken = null;
        InviteExpiresAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Generates a secure random token.
    /// </summary>
    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    /// <summary>
    /// Generates a unique 6-character session code.
    /// Excludes confusing characters: 0, O, I, 1.
    /// </summary>
    private static string GenerateSessionCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Exclude 0, O, I, 1
        var code = new char[6];

        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var randomBytes = new byte[6];
        rng.GetBytes(randomBytes);

        for (int i = 0; i < 6; i++)
        {
            code[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(code);
    }
}

/// <summary>
/// Session type enumeration.
/// </summary>
public enum SessionType
{
    /// <summary>
    /// Generic session without specific game reference.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// Session linked to a specific game.
    /// </summary>
    GameSpecific = 1
}

/// <summary>
/// Session status enumeration.
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// Session is currently active.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Session is paused (can be resumed).
    /// </summary>
    Paused = 1,

    /// <summary>
    /// Session is finalized (no further changes).
    /// </summary>
    Finalized = 2
}

/// <summary>
/// Participant entity (owned by Session).
/// </summary>
public class Participant
{
    /// <summary>
    /// Participant unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Session reference.
    /// </summary>
    public Guid SessionId { get; set; }

    /// <summary>
    /// Optional user reference for registered users.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Display name for the participant.
    /// </summary>
    [MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if participant is the session owner.
    /// </summary>
    public bool IsOwner { get; set; }

    /// <summary>
    /// Order in which participant joined (1-based).
    /// </summary>
    public int JoinOrder { get; set; }

    /// <summary>
    /// Final rank in the game (1 = winner).
    /// </summary>
    public int? FinalRank { get; set; }

    /// <summary>
    /// When participant joined.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Sets the final rank for this participant.
    /// </summary>
    internal void SetFinalRank(int rank)
    {
        if (rank <= 0)
            throw new ArgumentException("Rank must be positive.", nameof(rank));

        FinalRank = rank;
    }
}