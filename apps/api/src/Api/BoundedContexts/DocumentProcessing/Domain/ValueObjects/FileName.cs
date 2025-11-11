using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

public sealed class FileName : ValueObject
{
    public string Value { get; }

    public FileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ValidationException("File name cannot be empty");

        var trimmed = fileName.Trim();
        if (trimmed.Length > 255)
            throw new ValidationException("File name cannot exceed 255 characters");

        if (!trimmed.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("File must be a PDF (.pdf extension)");

        Value = trimmed;
    }

    public string GetNameWithoutExtension() => System.IO.Path.GetFileNameWithoutExtension(Value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }

    public override string ToString() => Value;
}
