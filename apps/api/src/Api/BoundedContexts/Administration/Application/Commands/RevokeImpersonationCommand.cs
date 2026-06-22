using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Superadmin kill-switch: forcibly revokes another admin's active impersonation session
/// (SP5 Admin Security S2 — T5 / D-S2-5). Distinct from <see cref="ImpersonationEndCommand"/>
/// (self-end by the impersonating admin): revoke is invoked by a DIFFERENT superadmin to
/// terminate an impersonation they did not start.
///
/// <para>Audit attribution is bespoke (written by the handler, NOT the generic
/// <c>[AuditableAction]</c> behavior) because the audit row's subject is the impersonating admin
/// (loaded from the session), not the caller: <c>user_id = session.ImpersonatedByUserId</c>
/// (the actor whose impersonation is killed), <c>impersonated_user_id = RequestingUserId</c>
/// (the superadmin pulling the kill-switch). See Scenario S2-4.</para>
///
/// <para>Not <c>[AtomicAudit]</c>: the revoke (RevokedAt write) is the security-critical action
/// and must succeed even if the audit enqueue hiccups — best-effort audit, revoke-first. The
/// revoke takes effect on the next request via invalidate-on-read (ValidateSessionQuery reads
/// RevokedAt every time; no cache), satisfying the ≤5s SLA.</para>
/// </summary>
internal record RevokeImpersonationCommand(
    Guid SessionId,
    Guid RequestingUserId
) : ICommand<bool>;
