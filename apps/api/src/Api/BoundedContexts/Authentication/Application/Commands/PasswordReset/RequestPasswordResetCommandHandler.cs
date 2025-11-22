using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;

/// <summary>
/// Handles password reset requests with CQRS pattern.
/// Business logic: Email validation → Rate limiting → Token generation → Email sending.
/// Infrastructure delegation: Email service and rate limiting.
/// Security: Always returns success to prevent email enumeration attacks.
/// </summary>
public sealed class RequestPasswordResetCommandHandler : ICommandHandler<RequestPasswordResetCommand, RequestPasswordResetResult>
{
    private readonly IPasswordResetService _passwordResetService;
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger;

    public RequestPasswordResetCommandHandler(
        IPasswordResetService passwordResetService,
        ILogger<RequestPasswordResetCommandHandler> logger)
    {
        _passwordResetService = passwordResetService ?? throw new ArgumentNullException(nameof(passwordResetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RequestPasswordResetResult> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Validate email format (basic business logic)
            if (string.IsNullOrWhiteSpace(command.Email))
            {
                return new RequestPasswordResetResult
                {
                    Success = true,
                    Message = "If the email exists, a password reset link has been sent"
                };
            }

            // Delegate to infrastructure service for token generation and email
            await _passwordResetService.RequestPasswordResetAsync(command.Email, cancellationToken);

            return new RequestPasswordResetResult
            {
                Success = true,
                Message = "If the email exists, a password reset link has been sent"
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Too many"))
        {
            // Rate limit exceeded - return generic message for security
            _logger.LogWarning("Password reset rate limit exceeded for email pattern");
            return new RequestPasswordResetResult
            {
                Success = false,
                Message = "Too many password reset requests. Please try again later."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during password reset request");
            // Return success to prevent information leakage
            return new RequestPasswordResetResult
            {
                Success = true,
                Message = "If the email exists, a password reset link has been sent"
            };
        }
    }
}
