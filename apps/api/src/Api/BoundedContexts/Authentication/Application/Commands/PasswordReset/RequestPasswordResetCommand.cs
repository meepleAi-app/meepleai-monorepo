using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;

/// <summary>
/// Command to initiate password reset flow.
/// Generates reset token and sends email to user.
/// </summary>
public sealed record RequestPasswordResetCommand : ICommand<RequestPasswordResetResult>
{
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// Result of password reset request.
/// Always returns success to prevent email enumeration attacks.
/// </summary>
public sealed record RequestPasswordResetResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
