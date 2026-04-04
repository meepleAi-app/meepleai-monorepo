namespace Api.SharedKernel.Exceptions;

/// <summary>
/// Exception thrown when a user exceeds a tier-based resource limit.
/// E2-3: Game Night Improvvisata — Enforce Tier Limits on Existing Endpoints.
/// Handled by <see cref="Api.Middleware.ApiExceptionHandlerMiddleware"/> → HTTP 403.
/// </summary>
public class TierLimitExceededException : Exception
{
    /// <summary>The type of limit that was exceeded (e.g., "UploadPdf", "CreateAgent").</summary>
    public string LimitType { get; }

    /// <summary>The user's current usage count.</summary>
    public int Current { get; }

    /// <summary>The maximum allowed by the user's tier.</summary>
    public int Max { get; }

    /// <summary>Client-facing URL to upgrade the subscription.</summary>
    public string UpgradeUrl { get; }

    public TierLimitExceededException(string limitType, int current, int max)
        : base($"Tier limit exceeded: {limitType} ({current}/{max})")
    {
        LimitType = limitType;
        Current = current;
        Max = max;
        UpgradeUrl = "/pricing";
    }

    public TierLimitExceededException(string limitType, string message)
        : base(message)
    {
        LimitType = limitType;
        Current = 0;
        Max = 0;
        UpgradeUrl = "/pricing";
    }

    public TierLimitExceededException() : base("Tier limit exceeded")
    {
        LimitType = "Unknown";
        Current = 0;
        Max = 0;
        UpgradeUrl = "/pricing";
    }

    public TierLimitExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
        LimitType = "Unknown";
        Current = 0;
        Max = 0;
        UpgradeUrl = "/pricing";
    }
}
