using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Starts an impersonation session: creates a new UserSession whose <c>UserId</c> is the
/// target (subject) and whose <c>ImpersonatedByUserId</c> is the requester (actor, a
/// superadmin). Successor to the legacy <c>ImpersonateUserCommand</c> from issues
/// #2890 / #3349.
///
/// <para>SP5 Admin Security S2 — T3. Replaces the legacy command which:
/// <list type="bullet">
/// <item>Used <c>IpAddress="impersonated"</c> as a magic-string signal (now redundant — the
///   new <c>impersonated_by_user_id</c> column carries this state explicitly).</item>
/// <item>Wrote 2 <c>audit_logs</c> rows manually via <c>IAuditLogRepository.AddAsync</c>,
///   bypassing the S1 outbox. The new command relies on <c>AuditLoggingBehavior</c> +
///   <c>[AtomicAudit]</c> to write a single Pending outbox row in the same transaction
///   as the session creation.</item>
/// <item>Accepted both <c>admin</c> and <c>superadmin</c> callers. The new command tightens
///   eligibility to <c>superadmin</c> only (D-S2-1).</item>
/// </list>
/// </para>
///
/// <para>Attributes:
/// <list type="bullet">
/// <item><c>[AuditableAction("ImpersonationStarted", "Session")]</c> with
///   <c>UserIdSource=ResourceId</c> so <c>audit_logs.user_id</c> = target (subject) and
///   <c>impersonated_user_id</c> = requester (actor). Convention D-S2-3.</item>
/// <item><c>[AtomicAudit]</c>: session row INSERT + outbox row commit atomically — either
///   both succeed or both roll back.</item>
/// <item><c>[RequireTwoFactor]</c>: high-risk action gated by step-up 2FA recency. Shadow
///   mode in S2 (warning only); strict mode in S3.</item>
/// </list>
/// </para>
/// </summary>
[AuditableAction("ImpersonationStarted", "Session",
    Level = 2,
    UserIdSource = AuditUserIdSource.ResourceId)]
[AtomicAudit]
// SP5 S3 — D-S3-7: tighter 5-min TOTP recency for impersonation (vs the 30-min default for
// DeleteUser/ChangeRole/Suspend). Impersonation grants a full target-user session, so it
// warrants the freshest step-up.
[RequireTwoFactor(MaxAgeMinutes = 5, Reason = "Impersonate other user; HIGH RISK — full target-user session granted.")]
internal record ImpersonationStartCommand(
    Guid TargetUserId,
    Guid RequestingUserId,
    string Reason,
    int DurationMinutes = 15
) : ICommand<ImpersonationStartResponseDto>;
