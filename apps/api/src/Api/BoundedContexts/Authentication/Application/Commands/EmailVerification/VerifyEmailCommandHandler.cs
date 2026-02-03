using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;

/// <summary>
/// Handles email verification using a token.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
internal sealed class VerifyEmailCommandHandler : ICommandHandler<VerifyEmailCommand, VerifyEmailResult>
{
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<VerifyEmailCommandHandler> _logger;

    public VerifyEmailCommandHandler(
        IEmailVerificationService emailVerificationService,
        ILogger<VerifyEmailCommandHandler> logger)
    {
        _emailVerificationService = emailVerificationService ?? throw new ArgumentNullException(nameof(emailVerificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VerifyEmailResult> Handle(VerifyEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            if (string.IsNullOrWhiteSpace(command.Token))
            {
                return new VerifyEmailResult
                {
                    Success = false,
                    Message = "Invalid verification token"
                };
            }

            var success = await _emailVerificationService.VerifyEmailAsync(command.Token, cancellationToken).ConfigureAwait(false);

            if (success)
            {
                return new VerifyEmailResult
                {
                    Success = true,
                    Message = "Email verified successfully"
                };
            }

            return new VerifyEmailResult
            {
                Success = false,
                Message = "Invalid or expired verification token"
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Catches unexpected infrastructure failures (DB, network) to prevent exception propagation
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during email verification");
            return new VerifyEmailResult
            {
                Success = false,
                Message = "An error occurred during email verification. Please try again."
            };
        }
#pragma warning restore CA1031
    }
}
