using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;

/// <summary>
/// Command to reset user password using valid reset token.
/// Validates token, updates password, and revokes all user sessions.
/// </summary>
internal sealed record ResetPasswordCommand : ICommand<ResetPasswordResult>
{
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// Result of password reset operation.
/// </summary>
internal sealed record ResetPasswordResult
{
    public bool Success { get; init; }
    public Guid? UserId { get; init; }
    public string? ErrorMessage { get; init; }
}
