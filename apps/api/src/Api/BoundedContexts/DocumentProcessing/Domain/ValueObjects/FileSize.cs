using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

public sealed class FileSize : ValueObject
{
    private const long MaxSizeBytes = 52428800; // 50 MB

    public long Bytes { get; }

    public FileSize(long bytes)
    {
        if (bytes < 0)
            throw new ValidationException("File size cannot be negative");

        if (bytes > MaxSizeBytes)
            throw new ValidationException($"File size cannot exceed {MaxSizeBytes / 1024 / 1024} MB");

        Bytes = bytes;
    }

    public double Kilobytes => Bytes / 1024.0;
    public double Megabytes => Bytes / 1024.0 / 1024.0;

    public bool IsLarge => Bytes > 10 * 1024 * 1024; // > 10 MB

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Bytes;
    }

    public override string ToString() =>
        Megabytes >= 1 ? $"{Megabytes:F2} MB" : $"{Kilobytes:F2} KB";
}
