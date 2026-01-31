namespace Api.SharedKernel.Domain.Exceptions;

/// <summary>
/// Exception thrown when a quota or limit has been exceeded.
/// Used for rate limiting, session limits, storage limits, etc.
/// Issue #3070: Created for session quota enforcement.
/// </summary>
public class QuotaExceededException : DomainException
{
    /// <summary>
    /// The type of quota that was exceeded (e.g., "SessionQuota", "StorageQuota", "RateLimit").
    /// </summary>
    public string QuotaType { get; }

    public QuotaExceededException(string quotaType, string message)
        : base(message)
    {
        QuotaType = quotaType;
    }

    public QuotaExceededException(string quotaType, string message, Exception innerException)
        : base(message, innerException)
    {
        QuotaType = quotaType;
    }
}
