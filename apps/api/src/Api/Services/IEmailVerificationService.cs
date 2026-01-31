namespace Api.Services;

/// <summary>
/// Service for email verification operations.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
internal interface IEmailVerificationService
{
    /// <summary>
    /// Generates and sends a verification token for a user after registration.
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="email">The email address to verify</param>
    /// <param name="displayName">The user's display name for the email</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the verification email was sent</returns>
    Task<bool> SendVerificationEmailAsync(Guid userId, string email, string displayName, CancellationToken ct = default);

    /// <summary>
    /// Verifies an email using the provided token.
    /// </summary>
    /// <param name="token">The verification token from the email</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if verification was successful</returns>
    Task<bool> VerifyEmailAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Resends a verification email to a user. Rate limited to 1 request per minute.
    /// </summary>
    /// <param name="email">The email address to resend verification to</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the verification email was resent</returns>
    Task<bool> ResendVerificationEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Checks if a user's email is verified.
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the user's email is verified</returns>
    Task<bool> IsEmailVerifiedAsync(Guid userId, CancellationToken ct = default);
}
