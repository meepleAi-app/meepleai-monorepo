using Api.BoundedContexts.Administration.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Ends an active impersonation session (self-end by the acting admin). Successor to the legacy
/// <c>EndImpersonationCommand</c> (issue #3349).
///
/// <para>SP5 Admin Security S2 — T4. Audit is written by <c>AuditLoggingBehavior</c> via
/// <c>[AuditableAction]</c> (the legacy manual <c>IAuditLogRepository.AddAsync</c> is dismantled,
/// issue #1534). Distinct from <c>ImpersonationAutoEnded</c> (middleware-emitted on expiry) and
/// <c>ImpersonationRevoked</c> (superadmin kill-switch, T5).</para>
///
/// <para>Not <c>[AtomicAudit]</c>: ending an impersonation is non-destructive — losing the audit
/// of a self-end is recoverable from telemetry (same best-effort rationale as S1 non-destructive
/// commands). <c>UserIdSource</c> defaults to <c>Caller</c> so <c>user_id</c> = the admin who
/// ended the session.</para>
/// </summary>
[AuditableAction("ImpersonationEnded", "Session", Level = 1)]
internal record ImpersonationEndCommand(
    Guid SessionId,
    Guid RequestingUserId
) : ICommand<bool>;
