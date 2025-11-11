namespace Api.Constants;

/// <summary>
/// Centralized file-related constants to eliminate magic numbers.
/// </summary>
public static class FileConstants
{
    /// <summary>
    /// 10 MB in bytes (10 * 1024 * 1024 = 10,485,760).
    /// Used for evaluation result file size limits.
    /// </summary>
    public const long TenMegabytes = 10 * 1024 * 1024;

    /// <summary>
    /// 50 MB in bytes (50 * 1024 * 1024 = 52,428,800).
    /// Maximum PDF file size for upload.
    /// </summary>
    public const long FiftyMegabytes = 50 * 1024 * 1024;

    /// <summary>
    /// Default maximum PDF file size (50 MB).
    /// </summary>
    public const long MaxPdfFileSizeBytes = FiftyMegabytes;
}
