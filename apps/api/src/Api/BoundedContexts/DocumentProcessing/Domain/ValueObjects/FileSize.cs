using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Guards;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// File size in bytes.
/// Represents a valid file size (minimum 1 byte).
/// Note: Maximum size validation is a business rule enforced by PdfValidationDomainService.
/// </summary>
internal sealed class FileSize : ValueObject
{
    /// <summary>
    /// Size in bytes (minimum 1)
    /// </summary>
    public long Bytes { get; }

    /// <summary>
    /// Size in kilobytes
    /// </summary>
    public double Kilobytes => Bytes / (double)FileSizeCategories.BytesPerKilobyte;

    /// <summary>
    /// Size in megabytes
    /// </summary>
    public double Megabytes => Bytes / (double)FileSizeCategories.BytesPerMegabyte;

    /// <summary>
    /// Size in gigabytes
    /// </summary>
    public double Gigabytes => Bytes / (double)FileSizeCategories.BytesPerGigabyte;

    /// <summary>
    /// Creates a file size with the specified byte count.
    /// </summary>
    /// <param name="bytes">File size in bytes (must be >= 1)</param>
    /// <exception cref="ValidationException">Thrown if size is invalid</exception>
    public FileSize(long bytes)
    {
        Guard.AgainstTooSmall(bytes, nameof(bytes), FileSizeCategories.MinimumFileSize);
        Bytes = bytes;
    }

    /// <summary>
    /// Creates a FileSize from megabytes.
    /// </summary>
    /// <param name="megabytes">Size in megabytes</param>
    /// <returns>FileSize instance</returns>
    public static FileSize FromMegabytes(double megabytes)
    {
        if (megabytes <= 0)
            throw new ValidationException("Megabytes must be greater than 0");

        var bytes = (long)(megabytes * FileSizeCategories.BytesPerMegabyte);
        return new FileSize(bytes);
    }

    /// <summary>
    /// Creates a FileSize from kilobytes.
    /// </summary>
    /// <param name="kilobytes">Size in kilobytes</param>
    /// <returns>FileSize instance</returns>
    public static FileSize FromKilobytes(double kilobytes)
    {
        if (kilobytes <= 0)
            throw new ValidationException("Kilobytes must be greater than 0");

        var bytes = (long)(kilobytes * FileSizeCategories.BytesPerKilobyte);
        return new FileSize(bytes);
    }

    /// <summary>
    /// Checks if the file size is within the specified limit (in bytes).
    /// </summary>
    /// <param name="maxBytes">Maximum allowed size in bytes</param>
    /// <returns>True if size &lt;= maxBytes</returns>
    public bool IsWithinLimit(long maxBytes)
    {
        if (maxBytes < 1)
            throw new ValidationException("Maximum size limit must be at least 1 byte");

        return Bytes <= maxBytes;
    }

    /// <summary>
    /// Checks if the file is very small (business definition: &lt; 1 KB).
    /// </summary>
    public bool IsVerySmall => Bytes < FileSizeCategories.BytesPerKilobyte;

    /// <summary>
    /// Checks if the file is small (business definition: &lt; 1 MB).
    /// </summary>
    public bool IsSmall => Bytes < FileSizeCategories.BytesPerMegabyte;

    /// <summary>
    /// Checks if the file is medium (business definition: 1-10 MB).
    /// </summary>
    public bool IsMedium => Bytes >= FileSizeCategories.BytesPerMegabyte && Bytes <= FileSizeCategories.MediumFileThreshold;

    /// <summary>
    /// Checks if the file is large (business definition: > 10 MB).
    /// </summary>
    public bool IsLarge => Bytes > FileSizeCategories.MediumFileThreshold;

    /// <summary>
    /// Returns a human-readable string representation of the file size.
    /// </summary>
    public override string ToString()
    {
        if (Bytes < FileSizeCategories.BytesPerKilobyte)
            return $"{Bytes} bytes";

        if (Bytes < FileSizeCategories.BytesPerMegabyte)
            return $"{Kilobytes:F1} KB";

        if (Bytes < FileSizeCategories.BytesPerGigabyte)
            return $"{Megabytes:F1} MB";

        return $"{Gigabytes:F2} GB";
    }

    /// <summary>
    /// Returns the size formatted as megabytes (e.g., "5.3 MB").
    /// </summary>
    public string ToMegabytesString() => $"{Megabytes:F1} MB";

    /// <summary>
    /// Equality comparison based on byte count.
    /// </summary>
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Bytes;
    }

    /// <summary>
    /// Implicit conversion to long for convenience.
    /// </summary>
#pragma warning disable S3877 // Null check is necessary for safety in implicit conversion
    public static implicit operator long(FileSize fileSize)
    {
        ArgumentNullException.ThrowIfNull(fileSize);
        return fileSize.Bytes;
    }
#pragma warning restore S3877

    /// <summary>
    /// Common file sizes as static constants.
    /// </summary>
    public static readonly FileSize OneByte = new(FileSizeCategories.MinimumFileSize);
    public static readonly FileSize OneKilobyte = new(FileSizeCategories.BytesPerKilobyte);
    public static readonly FileSize OneMegabyte = new(FileSizeCategories.BytesPerMegabyte);
}
