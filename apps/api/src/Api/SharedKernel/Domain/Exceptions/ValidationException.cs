namespace Api.SharedKernel.Domain.Exceptions;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Exception thrown when entity or value object validation fails.
/// </summary>
public class ValidationException : DomainException
{
    /// <summary>
    /// Gets the collection of validation errors.
    /// </summary>
    public required IDictionary<string, string[]> Errors { get; init; }

    [SetsRequiredMembers]
    public ValidationException() : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    [SetsRequiredMembers]
    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
    }

    [SetsRequiredMembers]
    public ValidationException(string message, Dictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }

    [SetsRequiredMembers]
    public ValidationException(string propertyName, string errorMessage)
        : base($"Validation failed for {propertyName}: {errorMessage}")
    {
        Errors = new Dictionary<string, string[]>
(StringComparer.Ordinal)
        {
            [propertyName] = new[] { errorMessage }
        };
    }
    public ValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
