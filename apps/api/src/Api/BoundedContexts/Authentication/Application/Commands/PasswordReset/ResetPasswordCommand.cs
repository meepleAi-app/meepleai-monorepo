using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;

/// <summary>
/// Command to reset user password using valid reset token.
/// Validates token, updates password, and revokes all user sessions.
/// </summary>
public sealed record ResetPasswordCommand : ICommand<ResetPasswordResult>
{
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// Result of password reset operation.
/// </summary>
public sealed record ResetPasswordResult
{
    public bool Success { get; init; }
    public Guid? UserId { get; init; }
    public string? ErrorMessage { get; init; }
}
