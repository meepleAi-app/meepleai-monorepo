namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Dual-principal session identity for SP5 Admin Security S2.
///
/// <para><see cref="Subject"/> is the user the system is "acting as" functionally — the owner of
/// resources, the target of rate-limits and quotas, the user whose preferences/profile/devices are
/// in scope. For a regular login session, <see cref="Subject"/> is simply the logged-in user.</para>
///
/// <para><see cref="Actor"/> is non-null IFF this session is an active impersonation; it represents
/// the admin/superadmin who initiated the impersonation. For a regular login session, <see cref="Actor"/>
/// is null.</para>
///
/// <para><b>Authorization checks and audit attribution MUST read <see cref="EffectiveActor"/></b>
/// (which returns <see cref="Actor"/> when impersonating, else <see cref="Subject"/>) — this
/// prevents privilege escalation/de-escalation via impersonation, and ensures audit rows
/// attribute the action to the real admin.</para>
///
/// <para><b>Resource ownership and rate-limit quotas MUST read <see cref="Subject"/></b> — an
/// admin impersonating a user must NOT bypass that user's quotas, and resource ownership
/// (own profile, own devices, own share links) is always relative to the subject.</para>
///
/// <para>Reference: three-amigos D-S2-2 (Option B: dual-principal record).
/// Spike inventory of the 99 functional call sites split by cluster:
/// <c>audits/2026-05-26-s2-spike-cluster-classification.md</c>.</para>
/// </summary>
internal sealed record Principal(UserDto Subject, UserDto? Actor)
{
    /// <summary>
    /// The real acting user: <see cref="Actor"/> when impersonating, else <see cref="Subject"/>.
    /// Use this for authorization checks (role/privilege) and audit attribution.
    /// </summary>
    public UserDto EffectiveActor => Actor ?? Subject;

    /// <summary>True when this session is an active impersonation (<see cref="Actor"/> is non-null).</summary>
    public bool IsImpersonating => Actor is not null;
}
