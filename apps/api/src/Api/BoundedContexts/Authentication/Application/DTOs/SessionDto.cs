

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
    Guid? SessionId = null
);
