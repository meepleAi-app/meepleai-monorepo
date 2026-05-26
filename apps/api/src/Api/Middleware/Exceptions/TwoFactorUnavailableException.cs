namespace Api.Middleware.Exceptions;

/// <summary>
/// Thrown when a 2FA verification cannot complete because the underlying TOTP store / rate-limit
/// backend (Redis or the encrypted-secret store) is unreachable. Mapped by
/// <c>ApiExceptionHandlerMiddleware</c> to <c>503 Service Unavailable</c> + body
/// <c>{ error: "two_factor_unavailable", message, correlationId, timestamp }</c>, so the FE can
/// surface a transient-error toast (retryable) instead of treating it as a failed code or a
/// step-up/enroll signal. SP5 Admin Security S3 — D-S3-4 (step-up failure mode).
/// </summary>
public sealed class TwoFactorUnavailableException : Exception
{
    public TwoFactorUnavailableException()
    {
    }

    public TwoFactorUnavailableException(string message)
        : base(message)
    {
    }

    public TwoFactorUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
