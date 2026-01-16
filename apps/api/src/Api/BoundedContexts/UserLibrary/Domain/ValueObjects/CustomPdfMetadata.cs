using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Value object representing metadata for a custom PDF rulebook uploaded by user.
/// Stores URL, upload timestamp, file size, and original filename.
/// </summary>
internal sealed class CustomPdfMetadata : ValueObject
{
    private const long MinFileSizeBytes = 1;
    private const long MaxFileSizeBytes = 100_000_000; // 100 MB limit
    private const int MaxFileNameLength = 255;

    /// <summary>
    /// URL where the custom PDF is stored (cloud storage or local path).
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// When the PDF was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; }

    /// <summary>
    /// Original filename provided by user.
    /// </summary>
    public string OriginalFileName { get; }

    /// <summary>
    /// Creates a new custom PDF metadata with validation.
    /// </summary>
    private CustomPdfMetadata(
        string url,
        DateTime uploadedAt,
        long fileSizeBytes,
        string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL cannot be empty", nameof(url));
        if (!Uri.TryCreate(url, UriKind.Absolute, out _) && !Uri.TryCreate(url, UriKind.Relative, out _))
            throw new ArgumentException("Invalid URL format", nameof(url));
        if (fileSizeBytes < MinFileSizeBytes || fileSizeBytes > MaxFileSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), $"File size must be between {MinFileSizeBytes} and {MaxFileSizeBytes} bytes");
        if (string.IsNullOrWhiteSpace(originalFileName))
            throw new ArgumentException("Original file name cannot be empty", nameof(originalFileName));
        if (originalFileName.Length > MaxFileNameLength)
            throw new ArgumentException($"File name cannot exceed {MaxFileNameLength} characters", nameof(originalFileName));

        Url = url.Trim();
        UploadedAt = uploadedAt;
        FileSizeBytes = fileSizeBytes;
        OriginalFileName = originalFileName.Trim();
    }

    /// <summary>
    /// Creates a new custom PDF metadata from parameters.
    /// </summary>
    public static CustomPdfMetadata Create(
        string url,
        long fileSizeBytes,
        string originalFileName)
    {
        return new CustomPdfMetadata(
            url: url,
            uploadedAt: DateTime.UtcNow,
            fileSizeBytes: fileSizeBytes,
            originalFileName: originalFileName
        );
    }

    /// <summary>
    /// Creates custom PDF metadata with explicit upload timestamp (for testing/migration).
    /// </summary>
    public static CustomPdfMetadata CreateWithTimestamp(
        string url,
        DateTime uploadedAt,
        long fileSizeBytes,
        string originalFileName)
    {
        return new CustomPdfMetadata(url, uploadedAt, fileSizeBytes, originalFileName);
    }

    /// <summary>
    /// Returns human-readable file size (KB, MB).
    /// </summary>
    public string GetFormattedFileSize()
    {
        const long kb = 1024;
        const long mb = kb * 1024;

        return FileSizeBytes switch
        {
            < kb => $"{FileSizeBytes} B",
            < mb => $"{FileSizeBytes / kb:N0} KB",
            _ => $"{FileSizeBytes / (double)mb:N2} MB"
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Url;
        yield return UploadedAt;
        yield return FileSizeBytes;
        yield return OriginalFileName;
    }

    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() =>
        $"CustomPdf[File={OriginalFileName}, Size={GetFormattedFileSize()}, Uploaded={UploadedAt:yyyy-MM-dd}]";
}
