namespace Api.BoundedContexts.DocumentProcessing.Domain.Enums;

/// <summary>
/// Categorizes PDF processing errors for targeted retry strategies.
/// Issue #4216: Error handling and manual retry mechanism.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Network-related errors: timeouts, connection failures, DNS issues.
    /// Retry strategy: Exponential backoff, high success rate on retry.
    /// </summary>
    Network = 1,

    /// <summary>
    /// PDF parsing errors: malformed PDF, unsupported format, corrupted file.
    /// Retry strategy: Low success rate, likely needs different extractor or manual fix.
    /// </summary>
    Parsing = 2,

    /// <summary>
    /// User quota exceeded: storage limit, document count limit, rate limit.
    /// Retry strategy: Only after user upgrades tier or quota resets.
    /// </summary>
    Quota = 3,

    /// <summary>
    /// External service unavailable: embedding service down, Qdrant unreachable.
    /// Retry strategy: Wait for service recovery, medium success rate.
    /// </summary>
    Service = 4,

    /// <summary>
    /// Unclassified error: unexpected exception, unknown failure mode.
    /// Retry strategy: Treat as transient, allow retry with caution.
    /// </summary>
    Unknown = 99
}
