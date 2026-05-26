namespace Api.Infrastructure.Entities;

/// <summary>
/// User session entity - persistence model.
/// DDD-PHASE2: Converted to Guid IDs for domain alignment.
/// SP5 Admin Security S2: added <see cref="ImpersonatedByUserId"/> + <see cref="ImpersonatedUntil"/>
/// to support the dual-principal impersonation flow (D-S2-2). A session row with
/// <see cref="ImpersonatedByUserId"/> non-null represents an active impersonation:
/// <see cref="UserId"/> is the SUBJECT (the user being acted-as) and <see cref="ImpersonatedByUserId"/>
/// is the ACTOR (the admin/superadmin who initiated the impersonation).
/// </summary>
public class UserSessionEntity
{
    required public Guid Id { get; set; }
    required public Guid UserId { get; set; }
    required public string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceFingerprint { get; set; } // Issue #3677: Device tracking

    /// <summary>
    /// Non-null when this session is an active impersonation. Holds the Id of the admin/superadmin
    /// that initiated the impersonation (the ACTOR). For regular login sessions, this is null.
    /// SP5 Admin Security S2 — D-S2-2 (dual-principal session).
    /// </summary>
    public Guid? ImpersonatedByUserId { get; set; }

    /// <summary>
    /// UTC timestamp at which the impersonation auto-expires (D-S2-4). Capped to
    /// <c>SystemConfiguration.ImpersonationMaxDurationMinutes</c> (default 15min). Null for
    /// non-impersonate sessions. The auth middleware enforces a hard 401 when this is in the past.
    /// SP5 Admin Security S2 — D-S2-4.
    /// </summary>
    public DateTime? ImpersonatedUntil { get; set; }

    required public UserEntity User { get; set; }
}

