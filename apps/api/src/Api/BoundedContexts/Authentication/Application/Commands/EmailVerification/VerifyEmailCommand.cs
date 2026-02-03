using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;

/// <summary>
/// Command to verify an email address using a verification token.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
internal sealed record VerifyEmailCommand : ICommand<VerifyEmailResult>
{
    public string Token { get; init; } = string.Empty;
}

/// <summary>
/// Result of email verification.
/// </summary>
internal sealed record VerifyEmailResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
