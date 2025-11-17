using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Handles 2FA code verification with CQRS pattern.
/// Business logic: Temp session validation → TOTP code verification → Backup code fallback.
/// Infrastructure delegation: Temp session management and TOTP crypto via services.
/// Security: Single-use temp sessions (5-min TTL), rate limiting handled at endpoint level.
/// </summary>
public sealed class Verify2FACommandHandler : ICommandHandler<Verify2FACommand, Verify2FAResult>
{
    private readonly ITotpService _totpService;
    private readonly ITempSessionService _tempSessionService;
    private readonly ILogger<Verify2FACommandHandler> _logger;

    public Verify2FACommandHandler(
        ITotpService totpService,
        ITempSessionService tempSessionService,
        ILogger<Verify2FACommandHandler> logger)
    {
        _totpService = totpService;
        _tempSessionService = tempSessionService;
        _logger = logger;
    }

    public async Task<Verify2FAResult> Handle(Verify2FACommand command, CancellationToken cancellationToken)
    {
        // Business logic validation
        if (string.IsNullOrWhiteSpace(command.SessionToken))
        {
            return new Verify2FAResult
            {
                Success = false,
                ErrorMessage = "Session token is required"
            };
        }

        if (string.IsNullOrWhiteSpace(command.Code))
        {
            return new Verify2FAResult
            {
                Success = false,
                ErrorMessage = "Verification code is required"
            };
        }

        // Step 1: Validate and consume temporary session (infrastructure - delegate to service)
        // Temp sessions are single-use with 5-minute TTL
        var userIdNullable = await _tempSessionService.ValidateAndConsumeTempSessionAsync(command.SessionToken);
        if (userIdNullable == null)
        {
            _logger.LogWarning("2FA verification failed: Invalid or expired temporary session");
            return new Verify2FAResult
            {
                Success = false,
                ErrorMessage = "Invalid or expired session token"
            };
        }

        var userId = userIdNullable.Value;

        // Step 2: Verify TOTP code (infrastructure - delegate to service)
        var isValid = await _totpService.VerifyCodeAsync(userId, command.Code);

        // Step 3: If TOTP fails, try backup code (infrastructure - delegate to service)
        if (!isValid)
        {
            isValid = await _totpService.VerifyBackupCodeAsync(userId, command.Code);
            if (isValid)
            {
                _logger.LogInformation("2FA verified using backup code for user {UserId}", userId);
            }
        }
        else
        {
            _logger.LogInformation("2FA verified using TOTP code for user {UserId}", userId);
        }

        if (!isValid)
        {
            _logger.LogWarning("2FA verification failed: Invalid code for user {UserId}", userId);
            return new Verify2FAResult
            {
                Success = false,
                ErrorMessage = "Invalid verification code"
            };
        }

        // Success - return user ID for session creation
        return new Verify2FAResult
        {
            Success = true,
            UserId = userId
        };
    }
}
