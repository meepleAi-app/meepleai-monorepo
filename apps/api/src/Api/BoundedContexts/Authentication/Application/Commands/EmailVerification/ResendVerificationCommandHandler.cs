using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;

/// <summary>
/// Handles resend verification email requests with rate limiting.
/// ISSUE-3071: Email verification backend implementation.
/// Security: Always returns success to prevent email enumeration attacks.
/// </summary>
internal sealed class ResendVerificationCommandHandler : ICommandHandler<ResendVerificationCommand, ResendVerificationResult>
{
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<ResendVerificationCommandHandler> _logger;

    public ResendVerificationCommandHandler(
        IEmailVerificationService emailVerificationService,
        ILogger<ResendVerificationCommandHandler> logger)
    {
        _emailVerificationService = emailVerificationService ?? throw new ArgumentNullException(nameof(emailVerificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResendVerificationResult> Handle(ResendVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            if (string.IsNullOrWhiteSpace(command.Email))
            {
                // Return success to prevent email enumeration
                return new ResendVerificationResult
                {
                    Success = true,
                    Message = "If the email exists and is not verified, a verification link has been sent"
                };
            }

            await _emailVerificationService.ResendVerificationEmailAsync(command.Email, cancellationToken).ConfigureAwait(false);

            return new ResendVerificationResult
            {
                Success = true,
                Message = "If the email exists and is not verified, a verification link has been sent"
            };
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Too many"))
        {
            // Rate limit exceeded - return specific message
            _logger.LogWarning(ex, "Verification resend rate limit exceeded");
            return new ResendVerificationResult
            {
                Success = false,
                Message = "Too many verification requests. Please try again later."
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Catches unexpected infrastructure failures to prevent exception propagation
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during verification resend");
            // Return success to prevent email enumeration
            return new ResendVerificationResult
            {
                Success = true,
                Message = "If the email exists and is not verified, a verification link has been sent"
            };
        }
#pragma warning restore CA1031
    }
}
