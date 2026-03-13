using System.Security.Cryptography;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Represents an invitation to join a live game session.
/// Supports PIN-based join (short code) and link-based join (UUID token).
/// E3-1: Session Invite Flow.
/// </summary>
public class SessionInvite
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string Pin { get; private set; } = null!;
    public string LinkToken { get; private set; } = null!;
    public int MaxUses { get; private set; }
    public int CurrentUses { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsable => !IsRevoked && !IsExpired && CurrentUses < MaxUses;

    private SessionInvite() { } // EF constructor

    public static SessionInvite Create(Guid sessionId, Guid createdByUserId, int maxUses, int expiryMinutes = 30)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("Session ID is required", nameof(sessionId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creator ID is required", nameof(createdByUserId));
        if (maxUses < 1) throw new ArgumentException("Max uses must be at least 1", nameof(maxUses));
        if (expiryMinutes < 1) throw new ArgumentException("Expiry must be at least 1 minute", nameof(expiryMinutes));

        return new SessionInvite
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            CreatedByUserId = createdByUserId,
            Pin = GeneratePin(),
            LinkToken = Guid.NewGuid().ToString("N"),
            MaxUses = maxUses,
            CurrentUses = 0,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
            IsRevoked = false
        };
    }

    public void RecordUse()
    {
        if (!IsUsable)
            throw new InvalidOperationException("Invite is no longer usable");
        CurrentUses++;
    }

    public void Revoke() => IsRevoked = true;

    private static string GeneratePin()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(RandomNumberGenerator.GetBytes(6)
            .Select(b => chars[b % chars.Length]).ToArray());
    }
}
