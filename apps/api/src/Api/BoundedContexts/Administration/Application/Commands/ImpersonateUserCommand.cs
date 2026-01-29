using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to impersonate user for debugging (Issue #2890).
/// HIGH SECURITY RISK: Creates full user session for admin debugging.
/// </summary>
internal record ImpersonateUserCommand(
    Guid TargetUserId,
    Guid AdminUserId
) : ICommand<ImpersonateUserResponseDto>;
