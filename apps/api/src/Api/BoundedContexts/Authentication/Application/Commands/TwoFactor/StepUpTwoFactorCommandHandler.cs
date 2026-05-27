using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Handles <see cref="StepUpTwoFactorCommand"/> (SP5 Admin Security S3 — T5).
///
/// Delegates verification to <see cref="ITotpService.VerifyCodeAsync"/> which already enforces
/// rate-limit, lockout, replay-prevention, constant-time comparison, and emits the
/// <c>TwoFactorVerify</c> audit — so this handler does NOT re-implement any of that. On success it
/// refreshes <c>LastTotpVerifiedAt</c> on the session (clearing a step-up block) and emits a
/// <c>TwoFactorStepUp</c> audit. A distinct lockout pre-check surfaces <c>LockedOut</c> so the
/// endpoint can return <c>401 two_factor_required</c> with <c>subcode=locked_out</c> +
/// <c>retryAfterSeconds</c> (D-S3-4b; unified with the enforcement vocabulary in Option B —
/// see refactor commit <c>399c98543</c> and <c>docs/api/2fa-step-up-protocol.md</c>).
/// </summary>
internal class StepUpTwoFactorCommandHandler : ICommandHandler<StepUpTwoFactorCommand, StepUpTwoFactorResult>
{
    // Mirrors TotpService.LockoutDurationMinutes (15min). The lockout key TTL is the authoritative
    // wait; this fixed value is the client-facing retry-after hint for the
    // 401 + subcode=locked_out response (Option B; not 429 — see ApiExceptionHandlerMiddleware).
    private const int LockoutRetryAfterSeconds = 900;

    private readonly ITotpService _totpService;
    private readonly ISessionRepository _sessionRepository;
    private readonly AuditService _auditService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StepUpTwoFactorCommandHandler> _logger;

    public StepUpTwoFactorCommandHandler(
        ITotpService totpService,
        ISessionRepository sessionRepository,
        AuditService auditService,
        TimeProvider timeProvider,
        ILogger<StepUpTwoFactorCommandHandler> logger)
    {
        _totpService = totpService ?? throw new ArgumentNullException(nameof(totpService));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StepUpTwoFactorResult> Handle(StepUpTwoFactorCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // D-S3-4b: surface lockout distinctly. VerifyCodeAsync also re-checks lockout, so this
        // pre-check is purely for the client-facing response — there is no security gap if the state
        // changes between the read and the verify. Both calls hit the Redis-backed TOTP store, so a
        // backend failure here means 2FA is unavailable (Option B → 503), not an invalid code.
        bool lockedOut;
        bool verified;
        try
        {
            lockedOut = await _totpService.IsLockedOutAsync(command.ActorUserId, cancellationToken).ConfigureAwait(false);
            // Short-circuit when locked out: do not call VerifyCodeAsync (avoids consuming an attempt).
            verified = !lockedOut
                && await _totpService.VerifyCodeAsync(command.ActorUserId, command.Code, cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031, S2221 // infrastructure boundary: any TotpService/Redis failure ⇒ 2FA unavailable
        catch (Exception ex)
#pragma warning restore CA1031, S2221
        {
            _logger.LogError(
                ex,
                "Step-up 2FA unavailable for actor {ActorUserId} on session {SessionId}: TOTP backend error",
                command.ActorUserId, command.SessionId);
            return new StepUpTwoFactorResult(StepUpOutcome.Unavailable);
        }

        if (lockedOut)
        {
            await _auditService.LogAsync(
                command.ActorUserId.ToString(),
                "TwoFactorStepUpLockout",
                "TwoFactor",
                command.SessionId.ToString(),
                "Blocked",
                "Step-up blocked: account locked out after excessive failed attempts",
                cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogWarning(
                "Step-up locked out for actor {ActorUserId} on session {SessionId}",
                command.ActorUserId, command.SessionId);

            return new StepUpTwoFactorResult(StepUpOutcome.LockedOut, RetryAfterSeconds: LockoutRetryAfterSeconds);
        }

        if (!verified)
        {
            // VerifyCodeAsync already audited TwoFactorVerify=Failed and tracked the attempt toward
            // the lockout threshold. Nothing else to do here.
            return new StepUpTwoFactorResult(StepUpOutcome.InvalidCode);
        }

        // Success — refresh the session's TOTP recency. ExecuteUpdate returns 0 if the session was
        // revoked/expired between auth and step-up; treat that as a failed step-up (the next request
        // would 401 on the invalid session anyway).
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var rowsAffected = await _sessionRepository
            .UpdateLastTotpVerifiedAtAsync(command.SessionId, now, cancellationToken).ConfigureAwait(false);
        if (rowsAffected == 0)
        {
            _logger.LogWarning(
                "Step-up verified but session {SessionId} no longer exists for actor {ActorUserId}",
                command.SessionId, command.ActorUserId);
            return new StepUpTwoFactorResult(StepUpOutcome.InvalidCode);
        }

        await _auditService.LogAsync(
            command.ActorUserId.ToString(),
            "TwoFactorStepUp",
            "TwoFactor",
            command.SessionId.ToString(),
            "Success",
            "Step-up TOTP verification succeeded; session recency refreshed",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new StepUpTwoFactorResult(StepUpOutcome.Success, LastTotpVerifiedAt: now);
    }
}
