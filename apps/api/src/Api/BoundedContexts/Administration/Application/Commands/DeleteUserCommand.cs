using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.BoundedContexts.Authentication.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to delete a user account.
/// Prevents self-deletion and deletion of the last admin.
/// </summary>
/// <remarks>
/// [AtomicAudit]: the mutation (user delete) and the audit_outbox row are written in a single
/// transaction. Either both commit or both roll back. This prevents the "ghost delete" scenario
/// where a user is deleted but no audit row is persisted, or an audit row is written for a
/// mutation that was subsequently rolled back. SP5 Admin Security S1 — Task 3b.
/// </remarks>
[AuditableAction("UserDelete", "User", Level = 2)]
[AtomicAudit]
[RequireTwoFactor(Reason = "Irreversible destruction of user data; must be 2FA-guarded.")]
internal record DeleteUserCommand(
    string UserId,
    string RequestingUserId
) : ICommand;
