using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to end an impersonation session (Issue #3349).
/// Terminates the impersonated session and logs audit trail.
/// Note: Audit logging is handled manually in the handler (richer context).
/// </summary>
internal record EndImpersonationCommand(
    Guid SessionId,
    Guid AdminUserId
) : ICommand<bool>;
