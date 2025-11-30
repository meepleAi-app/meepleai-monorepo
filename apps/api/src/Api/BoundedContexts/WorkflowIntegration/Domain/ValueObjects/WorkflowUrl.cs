using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.WorkflowIntegration.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated workflow URL.
/// </summary>
public sealed class WorkflowUrl : ValueObject
{
    public string Value { get; }

    public WorkflowUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ValidationException("Workflow URL cannot be empty");

        var trimmed = url.Trim();

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new ValidationException($"Invalid URL format: {trimmed}");

        if (!string.Equals(uri.Scheme, "http", StringComparison.Ordinal) && !string.Equals(uri.Scheme, "https", StringComparison.Ordinal))
            throw new ValidationException("URL must use HTTP or HTTPS protocol");

        Value = trimmed;
    }

    public Uri ToUri() => new Uri(Value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value.ToLowerInvariant();
    }

    public override string ToString() => Value;

    public static implicit operator string(WorkflowUrl url) => url.Value;
}
