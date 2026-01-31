using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.Authentication.Application.Queries.PasswordReset;

/// <summary>
/// Query to validate password reset token.
/// Checks if token exists, is not used, and is not expired.
/// </summary>
internal sealed record ValidatePasswordResetTokenQuery : IQuery<ValidatePasswordResetTokenResult>
{
    public string Token { get; init; } = string.Empty;
}

/// <summary>
/// Result of password reset token validation.
/// </summary>
internal sealed record ValidatePasswordResetTokenResult
{
    public bool IsValid { get; init; }
}
