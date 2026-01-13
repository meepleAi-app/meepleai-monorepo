using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;

/// <summary>
/// Handles password reset with CQRS pattern.
/// Business logic: Token validation → Password complexity check → Password update → Session revocation.
/// Infrastructure delegation: Database access and password hashing via password reset service.
/// Security: All user sessions are revoked after successful password reset.
/// </summary>
internal sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, ResetPasswordResult>
{
    private readonly IPasswordResetService _passwordResetService;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IPasswordResetService passwordResetService,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _passwordResetService = passwordResetService ?? throw new ArgumentNullException(nameof(passwordResetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResetPasswordResult> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            // Basic validation (business logic)
            if (string.IsNullOrWhiteSpace(command.Token))
            {
                return new ResetPasswordResult
                {
                    Success = false,
                    ErrorMessage = "Reset token is required"
                };
            }

            if (string.IsNullOrWhiteSpace(command.NewPassword))
            {
                return new ResetPasswordResult
                {
                    Success = false,
                    ErrorMessage = "New password is required"
                };
            }

            // Delegate to infrastructure service for password reset
            var (success, userId) = await _passwordResetService.ResetPasswordAsync(
                command.Token,
                command.NewPassword,
                cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                return new ResetPasswordResult
                {
                    Success = false,
                    ErrorMessage = "Invalid or expired reset token"
                };
            }

            _logger.LogInformation("Password reset completed successfully for user {UserId}", userId);

            return new ResetPasswordResult
            {
                Success = true,
                UserId = userId
            };
        }
        catch (ArgumentException ex)
        {
            // Password validation failed
            _logger.LogWarning(ex, "Password reset failed: {Error}", ex.Message);
            return new ResetPasswordResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific exceptions (ArgumentException) caught separately above.
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result pattern.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during password reset");
            return new ResetPasswordResult
            {
                Success = false,
                ErrorMessage = "An unexpected error occurred while resetting password"
            };
        }
#pragma warning restore CA1031
    }
}
