using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Validation;

namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

public sealed class FileName : ValueObject
{
    public string Value { get; }

    public FileName(string fileName)
    {
        var trimmed = fileName
            .NotNullOrWhiteSpace(nameof(fileName), "File name cannot be empty")
            .Then(f => f.Trim().MaxLength(255, nameof(fileName), "File name cannot exceed 255 characters"))
            .Then(f => f.Must(
                name => name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase),
                "File must be a PDF (.pdf extension)"))
            .ThrowIfFailure(nameof(FileName));

        Value = trimmed;
    }

    public string GetNameWithoutExtension() => System.IO.Path.GetFileNameWithoutExtension(Value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }

    public override string ToString() => Value;
}
