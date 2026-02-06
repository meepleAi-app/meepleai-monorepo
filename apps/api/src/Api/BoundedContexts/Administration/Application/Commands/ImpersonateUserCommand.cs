using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to impersonate user for debugging (Issue #2890).
/// HIGH SECURITY RISK: Creates full user session for admin debugging.
/// Note: Audit logging is handled manually in the handler (richer context with separate admin/target entries).
/// </summary>
internal record ImpersonateUserCommand(
    Guid TargetUserId,
    Guid AdminUserId
) : ICommand<ImpersonateUserResponseDto>;
