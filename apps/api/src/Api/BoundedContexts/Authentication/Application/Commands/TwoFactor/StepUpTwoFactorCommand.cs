using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command + Result + enum
namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Outcome of a step-up TOTP verification. The endpoint maps these to HTTP via the shared 2FA error
/// vocabulary (SP5 S3 — Option B, aligned with the enforcement filter): Success → 200; InvalidCode →
/// 401 <c>two_factor_required</c>/<c>invalid_code</c>; LockedOut → 401 <c>…</c>/<c>locked_out</c> +
/// retryAfterSeconds; Unavailable → 503 <c>two_factor_unavailable</c>.
/// Wire contract: <c>docs/api/2fa-step-up-protocol.md</c>. SP5 Admin Security S3 — D-S3-4.
/// </summary>
internal enum StepUpOutcome
{
    Success,
    InvalidCode,
    LockedOut,
    Unavailable
}

/// <summary>
/// Refreshes the TOTP recency (<c>LastTotpVerifiedAt</c>) on the CURRENT session after the actor
/// re-verifies their authenticator code. This is the step-up flow that clears a
/// <c>step_up_required</c> block from <c>TwoFactorEnforcementBehavior</c> (T4) — it does NOT
/// create a new session (unlike the login-time <c>/auth/2fa/verify</c>).
///
/// <para><see cref="ActorUserId"/> is the EffectiveActor of the session (the impersonating admin
/// during an impersonation, else the user). <see cref="SessionId"/> is the session row whose
/// recency is refreshed. Both are resolved by the endpoint from the validated session, keeping
/// this handler free of HttpContext coupling.</para>
///
/// SP5 Admin Security S3 — D-S3-4.
/// </summary>
internal record StepUpTwoFactorCommand(
    Guid SessionId,
    Guid ActorUserId,
    string Code) : ICommand<StepUpTwoFactorResult>;

internal record StepUpTwoFactorResult(
    StepUpOutcome Outcome,
    DateTime? LastTotpVerifiedAt = null,
    int? RetryAfterSeconds = null);
