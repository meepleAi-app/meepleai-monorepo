using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;

/// <summary>
/// Command to initiate password reset flow.
/// Generates reset token and sends email to user.
/// </summary>
internal sealed record RequestPasswordResetCommand : ICommand<RequestPasswordResetResult>
{
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// Result of password reset request.
/// Always returns success to prevent email enumeration attacks.
/// </summary>
internal sealed record RequestPasswordResetResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
