namespace Api.BoundedContexts.GameManagement.Application.Validation;

/// <summary>
/// Provides safe GUID parsing with validation and descriptive error messages.
/// </summary>
internal static class GuidValidator
{
    /// <summary>
    /// Parses a string to Guid with validation.
    /// </summary>
    /// <param name="value">String value to parse</param>
    /// <param name="parameterName">Name of the parameter for error messages</param>
    /// <returns>Parsed Guid</returns>
    /// <exception cref="ArgumentException">If value is null, empty, or not a valid GUID</exception>
    public static Guid ParseRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be null or empty", parameterName);
        }

        if (!Guid.TryParse(value, out var result))
        {
            throw new ArgumentException($"{parameterName} must be a valid GUID format", parameterName);
        }

        return result;
    }

    /// <summary>
    /// Parses a nullable string to nullable Guid with validation.
    /// </summary>
    /// <param name="value">String value to parse (can be null)</param>
    /// <param name="parameterName">Name of the parameter for error messages</param>
    /// <returns>Parsed Guid or null if value is null/empty</returns>
    /// <exception cref="ArgumentException">If value is not null/empty but not a valid GUID</exception>
    public static Guid? ParseOptional(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out var result))
        {
            throw new ArgumentException($"{parameterName} must be a valid GUID format", parameterName);
        }

        return result;
    }
}
