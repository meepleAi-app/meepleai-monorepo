namespace Api.SharedKernel.Constants;

/// <summary>
/// File size category thresholds for PDF documents.
/// </summary>
internal static class FileSizeCategories
{
    /// <summary>
    /// Bytes in one kilobyte (1024 bytes).
    /// </summary>
    public const long BytesPerKilobyte = 1024;

    /// <summary>
    /// Bytes in one megabyte (1024 * 1024 bytes).
    /// </summary>
    public const long BytesPerMegabyte = 1024 * 1024;

    /// <summary>
    /// Bytes in one gigabyte (1024 * 1024 * 1024 bytes).
    /// </summary>
    public const long BytesPerGigabyte = 1024 * 1024 * 1024;

    /// <summary>
    /// Threshold for "medium" files (10 MB).
    /// Files above this are considered "large".
    /// </summary>
    public const long MediumFileThreshold = 10 * BytesPerMegabyte;

    /// <summary>
    /// Minimum file size (1 byte).
    /// </summary>
    public const long MinimumFileSize = 1;
}
