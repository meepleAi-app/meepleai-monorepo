using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;

/// <summary>
/// Command to resend verification email.
/// Rate limited to 1 request per minute.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
internal sealed record ResendVerificationCommand : ICommand<ResendVerificationResult>
{
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// Result of resend verification request.
/// Always returns success to prevent email enumeration attacks.
/// </summary>
internal sealed record ResendVerificationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
