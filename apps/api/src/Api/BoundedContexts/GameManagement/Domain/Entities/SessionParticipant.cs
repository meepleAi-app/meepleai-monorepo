using System.Security.Cryptography;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Represents a participant in a live game session.
/// Supports both registered users (UserId set) and anonymous guests (GuestName set).
/// </summary>
public class SessionParticipant
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? GuestName { get; private set; }
    public ParticipantRole Role { get; private set; }
    public bool AgentAccessEnabled { get; private set; }
    public string ConnectionToken { get; private set; } = null!;
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }

    public string DisplayName => GuestName ?? "User";
    public bool IsRegisteredUser => UserId.HasValue;
    public bool IsActive => LeftAt == null;

    private SessionParticipant() { } // EF constructor

    /// <summary>
    /// Creates a guest participant (no registered user account).
    /// </summary>
    public static SessionParticipant CreateGuest(Guid sessionId, string guestName, ParticipantRole role)
    {
        if (string.IsNullOrWhiteSpace(guestName))
            throw new ArgumentException("Guest name is required", nameof(guestName));

        return new SessionParticipant
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            GuestName = guestName.Trim(),
            Role = role,
            AgentAccessEnabled = false,
            ConnectionToken = GeneratePin(),
            JoinedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a registered user participant.
    /// Hosts automatically get agent access enabled.
    /// </summary>
    public static SessionParticipant CreateRegistered(Guid sessionId, Guid userId, ParticipantRole role)
    {
        return new SessionParticipant
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            Role = role,
            AgentAccessEnabled = role == ParticipantRole.Host,
            ConnectionToken = GeneratePin(),
            JoinedAt = DateTime.UtcNow
        };
    }

    public void EnableAgentAccess() => AgentAccessEnabled = true;
    public void DisableAgentAccess() => AgentAccessEnabled = false;
    public void Leave() => LeftAt = DateTime.UtcNow;

    private static string GeneratePin()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous O/0/I/1/l (32 chars = zero modulo bias from byte % 32)
        return new string(RandomNumberGenerator.GetBytes(6)
            .Select(b => chars[b % chars.Length]).ToArray());
    }
}
