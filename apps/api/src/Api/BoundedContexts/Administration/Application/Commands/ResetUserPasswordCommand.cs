using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to reset a user's password.
/// </summary>
public record ResetUserPasswordCommand(
    string UserId,
    string NewPassword
) : ICommand;
