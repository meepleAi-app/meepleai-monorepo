using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.SecurityAudit.Application.Services;
using System.Text.Json;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Handles 2FA code verification with CQRS pattern.
/// Business logic: Temp session validation → TOTP code verification → Backup code fallback.
/// Infrastructure delegation: Temp session management and TOTP crypto via services.
/// Security: Single-use temp sessions (5-min TTL), rate limiting handled at endpoint level.
///
/// C6: the validate / consume / record-failure operations on the temp session
/// are now distinct, so a wrong code can be tracked toward the brute-force
/// invalidation threshold without consuming the session on the first failure.
/// User.RecordSuccessfulLogin is also moved here from LoginCommandHandler so
/// the failed-login counter is only reset after the second factor passes —
/// otherwise a successful password but failed 2FA reopened the brute-force
/// budget on every re-login.
/// </summary>
internal sealed class Verify2FACommandHandler : ICommandHandler<Verify2FACommand, Verify2FAResult>
{
    private readonly ITotpService _totpService;
    private readonly ITempSessionService _tempSessionService;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<Verify2FACommandHandler> _logger;
    private readonly IAuditLogger _auditLogger;

    public Verify2FACommandHandler(
        ITotpService totpService,
        ITempSessionService tempSessionService,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<Verify2FACommandHandler> logger,
        IAuditLogger auditLogger)
    {
        _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
        _tempSessionService = tempSessionService ?? throw new ArgumentNullException(nameof(tempSessionService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    }

    public async Task<Verify2FAResult> Handle(Verify2FACommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

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

        // Step 1 — validate temp session WITHOUT consuming. The session must
        // remain alive across a wrong-code attempt so the caller can record
        // a failure against the same row.
        var userIdNullable = await _tempSessionService
            .ValidateTempSessionAsync(command.SessionToken, cancellationToken)
            .ConfigureAwait(false);

        if (userIdNullable == null)
        {
            _logger.LogWarning("2FA verification failed: invalid, expired, or invalidated temporary session");
            return new Verify2FAResult
            {
                Success = false,
                ErrorMessage = "Invalid or expired session token"
            };
        }

        var userId = userIdNullable.Value;

        // Step 2 — verify TOTP, then backup code as fallback.
        var isValid = await _totpService.VerifyCodeAsync(userId, command.Code, cancellationToken).ConfigureAwait(false);
        if (!isValid)
        {
            isValid = await _totpService.VerifyBackupCodeAsync(userId, command.Code, cancellationToken).ConfigureAwait(false);
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
            // Step 3a — record the failed attempt. Returns true iff this
            // attempt invalidated the session (5th miss); the user must then
            // re-login. Below threshold the session stays alive so the user
            // can correct a typo without losing the password-validated state.
            var nowInvalidated = await _tempSessionService
                .RecordFailedAttemptAsync(command.SessionToken, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogWarning(
                "2FA verification failed: Invalid code for user {UserId} (invalidated: {Invalidated})",
                userId, nowInvalidated);

            // #3838: a wrong second factor is a failed login, like a wrong password.
            await _auditLogger.LogAsync(
                AuditEventType.LoginFailure,
                targetUserId: userId,
                metadata: JsonSerializer.Serialize(new
                {
                    reason = "invalid_second_factor",
                    sessionInvalidated = nowInvalidated,
                }),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return new Verify2FAResult
            {
                Success = false,
                ErrorMessage = nowInvalidated
                    ? "Too many failed attempts. Please log in again."
                    : "Invalid verification code"
            };
        }

        // Step 3b — success path: consume the temp session and reset the
        // failed-login counter on the user aggregate. C6 moves this counter
        // reset out of LoginCommandHandler so it only fires after the second
        // factor actually passes.
        await _tempSessionService
            .ConsumeTempSessionAsync(command.SessionToken, cancellationToken)
            .ConfigureAwait(false);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        // #5036: RecordSuccessfulLogin changes only the lockout state, so only that is written, and only when there is
        // something to clear. A full UpdateAsync would copy the aggregate read here over a suspension, a password reset
        // or a role change an admin saved in the meantime.
        if (user is not null && (user.FailedLoginAttempts > 0 || user.LockedUntil != null))
        {
            user.RecordSuccessfulLogin();
            await _userRepository.UpdateLockoutStateAsync(user, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // #3838: with 2FA on, this is where the login succeeds (LoginCommandHandler logs it only when no
        // second factor is needed).
        await _auditLogger.LogAsync(
            AuditEventType.LoginSuccess,
            actorUserId: userId,
            targetUserId: userId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new Verify2FAResult
        {
            Success = true,
            UserId = userId
        };
    }
}
