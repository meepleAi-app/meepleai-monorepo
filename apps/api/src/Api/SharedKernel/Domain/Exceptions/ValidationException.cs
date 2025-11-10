namespace Api.SharedKernel.Domain.Exceptions;

/// <summary>
/// Exception thrown when entity or value object validation fails.
/// </summary>
public class ValidationException : DomainException
{
    /// <summary>
    /// Gets the collection of validation errors.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException() : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(string message, Dictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }

    public ValidationException(string propertyName, string errorMessage)
        : base($"Validation failed for {propertyName}: {errorMessage}")
    {
        Errors = new Dictionary<string, string[]>
        {
            [propertyName] = new[] { errorMessage }
        };
    }
}
