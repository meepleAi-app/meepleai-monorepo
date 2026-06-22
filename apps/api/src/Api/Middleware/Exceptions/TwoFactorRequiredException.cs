namespace Api.Middleware.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Discriminator of the reason a 2FA-required command was rejected. The wire format
/// (snake_case) is the contract documented in <c>docs/api/2fa-step-up-protocol.md</c>;
/// the FE distinguishes between "open the step-up modal" vs "open the enroll prompt"
/// vs "show locked-out toast" based on this value. SP5 Admin Security S3 — D-S3-2.
/// </summary>
public enum TwoFactorRequiredSubcode
{
    /// <summary>
    /// The user has 2FA enabled but the most recent TOTP verification on their session is
    /// stale (older than the per-command <c>MaxAgeMinutes</c>) or missing. The FE should
    /// open the step-up modal and retry the original request on success.
    /// </summary>
    StepUpRequired,

    /// <summary>
    /// The user does NOT have 2FA enabled at all. Hard block per D-S3-5; the FE should
    /// route to the 2FA enrollment flow.
    /// </summary>
    EnrollRequired,

    /// <summary>
    /// Step-up attempts have been throttled (5 fails in 5 min). The FE should show a
    /// retry-after toast; <see cref="TwoFactorRequiredException.RetryAfterSeconds"/>
    /// carries the wait. D-S3-4b.
    /// </summary>
    LockedOut,

    /// <summary>
    /// A step-up verification attempt failed: the submitted TOTP/backup code was invalid, expired,
    /// or already used. Distinct from <see cref="StepUpRequired"/> ("you must step up") — this means
    /// "the step-up you attempted did not succeed". Emitted by the step-up endpoint only. D-S3-4.
    /// </summary>
    InvalidCode
}

/// <summary>
/// Thrown by <c>TwoFactorEnforcementBehavior</c> (and the step-up endpoint) when a
/// <c>[RequireTwoFactor]</c>-decorated command is rejected. Mapped by
/// <c>ApiExceptionHandlerMiddleware</c> to <c>401 Unauthorized</c> + structured body
/// <c>{ error: "two_factor_required", subcode, retryAfterSeconds? }</c> + header
/// <c>WWW-Authenticate: TOTP-StepUp realm="meepleai-admin"</c>.
///
/// SP5 Admin Security S3 — D-S3-2.
/// </summary>
public sealed class TwoFactorRequiredException : Exception
{
    /// <summary>Reason discriminator surfaced to the client.</summary>
    public required TwoFactorRequiredSubcode Subcode { get; init; }

    /// <summary>
    /// Seconds the client should wait before retrying. Non-null only when
    /// <see cref="Subcode"/> is <see cref="TwoFactorRequiredSubcode.LockedOut"/>.
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    public TwoFactorRequiredException()
    {
    }

    [SetsRequiredMembers]
    public TwoFactorRequiredException(TwoFactorRequiredSubcode subcode, string message)
        : base(message)
    {
        Subcode = subcode;
    }

    [SetsRequiredMembers]
    public TwoFactorRequiredException(TwoFactorRequiredSubcode subcode, string message, int retryAfterSeconds)
        : base(message)
    {
        Subcode = subcode;
        RetryAfterSeconds = retryAfterSeconds;
    }

    /// <summary>
    /// Wire-format string used in the JSON response body (snake_case per D-S3-2).
    /// </summary>
    public string SubcodeWire => Subcode switch
    {
        TwoFactorRequiredSubcode.StepUpRequired => "step_up_required",
        TwoFactorRequiredSubcode.EnrollRequired => "enroll_required",
        TwoFactorRequiredSubcode.LockedOut => "locked_out",
        TwoFactorRequiredSubcode.InvalidCode => "invalid_code",
        _ => "step_up_required"   // defensive fallback; should never hit
    };
}
