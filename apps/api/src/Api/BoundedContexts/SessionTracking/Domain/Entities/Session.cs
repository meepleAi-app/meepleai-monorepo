using System.ComponentModel.DataAnnotations;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
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
    /// Game reference. Required for all sessions (BR: min 1 game per session).
    /// </summary>
    public Guid GameId { get; private set; }

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
    /// Turn order as JSON array of participant IDs. Null if not yet set.
    /// </summary>
    public string? TurnOrderJson { get; private set; }

    /// <summary>
    /// Method used to set the turn order: "Manual" | "Random".
    /// </summary>
    [MaxLength(16)]
    public string? TurnOrderMethod { get; private set; }

    /// <summary>
    /// Seed used when TurnOrderMethod=Random, for audit/reproducibility.
    /// </summary>
    public int? TurnOrderSeed { get; private set; }

    /// <summary>
    /// Zero-based index of the current player in the turn order.
    /// </summary>
    public int? CurrentTurnIndex { get; private set; }

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
    /// <param name="gameId">Required game reference (BR: min 1 game per session).</param>
    /// <param name="sessionType">Session type.</param>
    /// <param name="location">Optional location.</param>
    /// <param name="sessionDate">Optional session date (defaults to now).</param>
    /// <returns>New session instance.</returns>
    public static Session Create(
        Guid userId,
        Guid gameId,
        SessionType sessionType,
        string? location = null,
        DateTime? sessionDate = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID cannot be empty.", nameof(gameId));

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
            Role = participantInfo.IsOwner ? ParticipantRole.Host : ParticipantRole.Player,
            JoinOrder = _participants.Count + 1,
            CreatedAt = DateTime.UtcNow
        };

        _participants.Add(participant);
    }

    /// <summary>
    /// Removes a participant from the session (kick).
    /// Only works on active/paused sessions. Cannot remove the host.
    /// </summary>
    /// <param name="participantId">Participant to remove.</param>
    public void RemoveParticipant(Guid participantId)
    {
        if (Status == SessionStatus.Finalized)
            throw new ConflictException("Cannot remove participants from finalized session.");

        var participant = _participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new NotFoundException($"Participant {participantId} not found in session.");

        if (participant.IsOwner)
            throw new ConflictException("Cannot remove the session host.");

        _participants.Remove(participant);
    }

    /// <summary>
    /// Marks a participant as ready for the next phase/turn.
    /// </summary>
    /// <param name="participantId">Participant to mark ready.</param>
    public void MarkParticipantReady(Guid participantId)
    {
        if (Status != SessionStatus.Active)
            throw new ConflictException($"Cannot mark ready in session with status {Status}.");

        var participant = _participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new NotFoundException($"Participant {participantId} not found in session.");

        participant.IsReady = true;
    }

    /// <summary>
    /// Resets all participants' ready state (e.g., after advancing a phase).
    /// </summary>
    public void ResetAllReady()
    {
        foreach (var participant in _participants)
        {
            participant.IsReady = false;
        }
    }

    /// <summary>
    /// Changes a participant's role. Only a Host can change roles.
    /// Cannot demote the last Host.
    /// </summary>
    /// <param name="participantId">Participant whose role is changing.</param>
    /// <param name="newRole">The new role to assign.</param>
    /// <param name="requestedBy">User ID of the requester (must be Host).</param>
    public void AssignParticipantRole(Guid participantId, ParticipantRole newRole, Guid requestedBy)
    {
        if (Status == SessionStatus.Finalized)
            throw new ConflictException("Cannot change roles in a finalized session.");

        var requester = _participants.FirstOrDefault(p => p.UserId == requestedBy)
            ?? throw new NotFoundException($"Requester {requestedBy} is not a participant.");

        if (requester.Role != ParticipantRole.Host)
            throw new ForbiddenException("Only hosts can assign roles.");

        var participant = _participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new NotFoundException($"Participant {participantId} not found in session.");

        // Cannot demote the last Host
        if (participant.Role == ParticipantRole.Host && newRole != ParticipantRole.Host)
        {
            var hostCount = _participants.Count(p => p.Role == ParticipantRole.Host);
            if (hostCount <= 1)
                throw new ConflictException("Cannot demote the last host. Promote another participant first.");
        }

        participant.Role = newRole;
        // Sync IsOwner flag with Host role
        participant.IsOwner = newRole == ParticipantRole.Host;
        UpdatedAt = DateTime.UtcNow;
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
    /// Sets the turn order for this session.
    /// </summary>
    /// <param name="method">Manual or Random.</param>
    /// <param name="order">Ordered participant IDs.</param>
    /// <param name="seed">Required when method is Random (for audit).</param>
    public void SetTurnOrder(
        Api.BoundedContexts.SessionTracking.Domain.Enums.TurnOrderMethod method,
        IReadOnlyList<Guid> order,
        int? seed)
    {
        if (Status == SessionStatus.Finalized)
            throw new ConflictException("Cannot set turn order on finalized session.");

        ArgumentNullException.ThrowIfNull(order);
        if (order.Count == 0)
            throw new ArgumentException("Turn order cannot be empty.", nameof(order));

        var participantIds = _participants.Select(p => p.Id).ToHashSet();
        foreach (var id in order)
        {
            if (!participantIds.Contains(id))
                throw new ArgumentException($"Participant {id} is not in this session.", nameof(order));
        }

        if (method == Api.BoundedContexts.SessionTracking.Domain.Enums.TurnOrderMethod.Random && !seed.HasValue)
            throw new ArgumentException("Random turn order requires a seed for audit.", nameof(seed));

        TurnOrderJson = System.Text.Json.JsonSerializer.Serialize(order);
        TurnOrderMethod = method.ToString();
        TurnOrderSeed = seed;
        CurrentTurnIndex = 0;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Advances the turn to the next player in the turn order (cyclic).
    /// Requires the session to be Active and a turn order to be set.
    /// Plan 1bis — T1.
    /// </summary>
    /// <returns>
    /// A tuple describing the transition: the prior index, the new index, and
    /// the corresponding participant IDs (used by the command handler to emit
    /// a <c>turn_advanced</c> diary event).
    /// </returns>
    public AdvanceTurnResult AdvanceTurn()
    {
        if (Status != SessionStatus.Active)
            throw new ConflictException($"Cannot advance turn in session with status {Status}.");

        if (string.IsNullOrEmpty(TurnOrderJson) || !CurrentTurnIndex.HasValue)
            throw new ConflictException("Turn order is not set for this session.");

        var order = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(TurnOrderJson)
            ?? new List<Guid>();
        if (order.Count == 0)
            throw new ConflictException("Turn order is empty.");

        var fromIndex = CurrentTurnIndex.Value;
        var toIndex = (fromIndex + 1) % order.Count;
        var fromParticipantId = order[fromIndex];
        var toParticipantId = order[toIndex];

        CurrentTurnIndex = toIndex;
        UpdatedAt = DateTime.UtcNow;

        return new AdvanceTurnResult(fromIndex, toIndex, fromParticipantId, toParticipantId);
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
/// Result of a <see cref="Session.AdvanceTurn"/> transition, carrying the
/// prior and next turn indices together with the corresponding participant
/// IDs so that command handlers can emit a diary event.
/// Plan 1bis — T1.
/// </summary>
public sealed record AdvanceTurnResult(
    int FromIndex,
    int ToIndex,
    Guid FromParticipantId,
    Guid ToParticipantId);

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
    /// Participant role in the session (Host, Player, Spectator).
    /// Determines which actions the participant can perform.
    /// </summary>
    public ParticipantRole Role { get; set; } = ParticipantRole.Player;

    /// <summary>
    /// Whether this participant is ready for the next phase/turn.
    /// </summary>
    public bool IsReady { get; set; }

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