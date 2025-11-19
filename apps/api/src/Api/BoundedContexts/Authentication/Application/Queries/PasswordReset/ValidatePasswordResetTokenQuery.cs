using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.PasswordReset;

/// <summary>
/// Query to validate password reset token.
/// Checks if token exists, is not used, and is not expired.
/// </summary>
public sealed record ValidatePasswordResetTokenQuery : IQuery<ValidatePasswordResetTokenResult>
{
    public string Token { get; init; } = string.Empty;
}

/// <summary>
/// Result of password reset token validation.
/// </summary>
public sealed record ValidatePasswordResetTokenResult
{
    public bool IsValid { get; init; }
}
