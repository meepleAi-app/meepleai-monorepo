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
    /// External service unavailable: embedding service down, pgvector unreachable.
    /// Retry strategy: Wait for service recovery, medium success rate.
    /// </summary>
    Service = 4,

    /// <summary>
    /// File exceeds the extraction service's size limit (e.g. HTTP 413).
    /// Retry strategy: Never — permanent, the file will not shrink. Requires a capacity
    /// decision (raise the service limit, split/compress the PDF) or operator action.
    /// Issue #3589.
    /// </summary>
    PayloadTooLarge = 5,

    /// <summary>
    /// The stored object is not at the key recorded in <c>FilePath</c>: the upload never reached the
    /// bucket, or the object was removed out of band.
    /// Retry strategy: Never — an absent object does not reappear on its own. Requires re-uploading
    /// the file or an operator restoring it. Issue #3846.
    /// </summary>
    StorageObjectMissing = 6,

    /// <summary>
    /// Unclassified error: unexpected exception, unknown failure mode.
    /// Retry strategy: Treat as transient, allow retry with caution.
    /// </summary>
    Unknown = 99
}
