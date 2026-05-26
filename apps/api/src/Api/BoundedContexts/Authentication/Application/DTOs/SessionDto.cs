

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
/// DTO for session status check.
/// Issue #3340: Added SessionId for device tracking.
///
/// SP5 Admin Security S2 — D-S2-2: the legacy <c>User: UserDto?</c> primary-constructor parameter
/// has been replaced by <see cref="Principal"/> (dual-principal record). A bridge property
/// <see cref="User"/> is retained <see cref="System.ObsoleteAttribute">[Obsolete]</see> during the
/// Wave 1+2 codemod (T2) so the ~349 pre-S2 consumers compile unchanged. To be removed in T2 step 4.
/// </summary>
internal record SessionStatusDto(
    bool IsValid,
    Principal? Principal,
    DateTime? ExpiresAt,
    DateTime? LastSeenAt,
    Guid? SessionId = null
)
{
    /// <summary>
    /// ⚠ DEPRECATED — bridge accessor for the S2 codemod transition only. Equivalent to
    /// <c>Principal?.Subject</c>. Kept TEMPORARILY so the ~349 pre-S2 consumers compile during
    /// the Wave 1+2 codemod (SP5 S2 T2). To be REMOVED after the codemod completes.
    ///
    /// <para>New code MUST use:</para>
    /// <list type="bullet">
    /// <item><c>Principal.Subject</c> for ownership/identity and rate-limit/quota keys (Clusters C+D)</item>
    /// <item><c>Principal.EffectiveActor</c> for authorization checks and audit attribution (Clusters A+B)</item>
    /// <item><c>Principal.IsImpersonating</c> for UI banners / log signals</item>
    /// </list>
    ///
    /// <para>Note: a <c>[System.Obsolete]</c> attribute would normally guard this, but the project
    /// treats CS0618 as an error — adding it now would block T1 commit. The codemod (T2) is
    /// driven by grep against the cluster classification in
    /// <c>audits/2026-05-26-s2-spike-cluster-classification.md</c> §3.</para>
    /// </summary>
    public UserDto? User => Principal?.Subject;
}
