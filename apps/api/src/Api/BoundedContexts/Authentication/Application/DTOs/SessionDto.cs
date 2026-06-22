

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Data transfer object for session information.
/// </summary>
internal record SessionDto(
    Guid Id,
    Guid UserId,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSeenAt,
    DateTime? RevokedAt,
    string? IpAddress,
    string? UserAgent,
    bool IsActive
);

/// <summary>
/// DTO for session status check. Issue #3340: Added SessionId for device tracking.
///
/// SP5 Admin Security S2 — D-S2-2: dual-principal session. The legacy <c>User: UserDto?</c>
/// primary-constructor parameter has been replaced by <see cref="Principal"/>. Consumers MUST
/// read:
/// <list type="bullet">
/// <item><c>Principal.Subject</c> for ownership/identity and rate-limit/quota keys (Clusters C+D)</item>
/// <item><c>Principal.EffectiveActor</c> for authorization checks and audit attribution (Clusters A+B)</item>
/// <item><c>Principal.IsImpersonating</c> for UI banners / log signals</item>
/// </list>
/// See <c>audits/2026-05-26-s2-spike-cluster-classification.md</c> §3 for the cluster classification
/// and the codemod that closed the transition. The transitional bridge property <c>User</c> was
/// removed at the end of T2 once the 107-file refactor was verified green.
/// </summary>
internal record SessionStatusDto(
    bool IsValid,
    Principal? Principal,
    DateTime? ExpiresAt,
    DateTime? LastSeenAt,
    Guid? SessionId = null,
    // SP5 S2 D-S2-4: set when a session that EXISTED was an impersonation and is now invalid
    // because its ImpersonatedUntil window elapsed. The auth middleware uses this to emit a
    // 401 + "ImpersonationAutoEnded" audit instead of failing open as a plain anonymous request.
    // Carries the subject + actor ids so the middleware can attribute the audit row.
    bool WasImpersonationAutoEnded = false,
    Guid? ImpersonationSubjectUserId = null,
    Guid? ImpersonationActorUserId = null,
    // SP5 S3 D-S3-3: TOTP recency, read by TwoFactorEnforcementBehavior strict path against
    // per-command MaxAgeMinutes. Null on (a) password-only login pre-S3, (b) sessions whose
    // step-up window has not been refreshed, (c) impersonate sessions where the actor had no
    // verification at the time of impersonation start (rare — ImpersonationStart is itself
    // [RequireTwoFactor(MaxAgeMinutes=5)] so a fresh value is expected to flow in).
    DateTime? LastTotpVerifiedAt = null
);
