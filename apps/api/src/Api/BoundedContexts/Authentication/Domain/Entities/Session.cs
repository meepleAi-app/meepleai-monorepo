using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Represents an authenticated user session.
/// Sessions have a limited lifetime and can be revoked.
/// Aggregate root for session lifecycle management.
/// </summary>
public sealed class Session : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? LastSeenAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    // Navigation property for EF Core
    public User? User { get; }

    /// <summary>
    /// Default session lifetime (30 days).
    /// </summary>
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private Session() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new session for a user.
    /// </summary>
    public Session(
        Guid id,
        Guid userId,
        SessionToken token,
        TimeSpan? lifetime = null,
        string? ipAddress = null,
        string? userAgent = null,
        TimeProvider? timeProvider = null) : base(id)
    {
        ArgumentNullException.ThrowIfNull(token);
        UserId = userId;
        TokenHash = token.ComputeHash();
        CreatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        ExpiresAt = CreatedAt.Add(lifetime ?? DefaultLifetime);
        IpAddress = ipAddress;
        // Truncate UserAgent to 256 chars to match database column constraint
        UserAgent = userAgent != null && userAgent.Length > 256
            ? userAgent.Substring(0, 256)
            : userAgent;
    }

    /// <summary>
    /// Checks if this session is still valid.
    /// </summary>
    public bool IsValid(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Session is valid if not expired and not revoked
        return now < ExpiresAt && RevokedAt == null;
    }

    /// <summary>
    /// Updates the last seen timestamp.
    /// </summary>
    public void UpdateLastSeen(TimeProvider? timeProvider = null)
    {
        LastSeenAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Revokes this session.
    /// </summary>
    public void Revoke(TimeProvider? timeProvider = null, string? reason = null)
    {
        if (RevokedAt != null)
            throw new DomainException("Session is already revoked");

        RevokedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        AddDomainEvent(new SessionRevokedEvent(Id, UserId, reason));
    }

    /// <summary>
    /// Checks if this session has expired.
    /// </summary>
    public bool IsExpired(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return timeProvider.GetUtcNow().UtcDateTime >= ExpiresAt;
    }

    /// <summary>
    /// Checks if this session was revoked.
    /// </summary>
    public bool IsRevoked() => RevokedAt != null;

    /// <summary>
    /// Extends the session lifetime by the specified duration.
    /// </summary>
    /// <param name="extension">Duration to extend the session by</param>
    /// <param name="timeProvider">Time provider for validation</param>
    public void Extend(TimeSpan extension, TimeProvider timeProvider)
    {
        if (IsRevoked())
            throw new DomainException("Cannot extend a revoked session");

        if (IsExpired(timeProvider))
            throw new DomainException("Cannot extend an expired session");

        if (extension <= TimeSpan.Zero)
            throw new DomainException("Extension duration must be positive");

        ExpiresAt = ExpiresAt.Add(extension);
        AddDomainEvent(new SessionExtendedEvent(Id, UserId, extension, ExpiresAt));
    }
}
