// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

namespace Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

/// <summary>
/// Result of validating a plugin configuration or input.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Validation errors, if any.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings (non-blocking issues).
    /// </summary>
    public IReadOnlyList<ValidationWarning> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a successful validation result with warnings.
    /// </summary>
    public static ValidationResult SuccessWithWarnings(params ValidationWarning[] warnings)
    {
        return new ValidationResult
        {
            IsValid = true,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(params ValidationError[] errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors
        };
    }

    /// <summary>
    /// Creates a failed validation result from error messages.
    /// </summary>
    public static ValidationResult Failure(params string[] errorMessages)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errorMessages.Select(m => new ValidationError { Message = m }).ToArray()
        };
    }

    /// <summary>
    /// Combines multiple validation results.
    /// </summary>
    public static ValidationResult Combine(params ValidationResult[] results)
    {
        var allErrors = results.SelectMany(r => r.Errors).ToList();
        var allWarnings = results.SelectMany(r => r.Warnings).ToList();

        return new ValidationResult
        {
            IsValid = allErrors.Count == 0,
            Errors = allErrors,
            Warnings = allWarnings
        };
    }
}

/// <summary>
/// A validation error that prevents execution.
/// </summary>
public sealed record ValidationError
{
    /// <summary>
    /// Error message describing the validation failure.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Property path that failed validation (e.g., "config.timeout").
    /// </summary>
    public string? PropertyPath { get; init; }

    /// <summary>
    /// Error code for programmatic handling.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// The invalid value, if available.
    /// </summary>
    public object? AttemptedValue { get; init; }
}

/// <summary>
/// A validation warning that doesn't prevent execution.
/// </summary>
public sealed record ValidationWarning
{
    /// <summary>
    /// Warning message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Property path the warning relates to.
    /// </summary>
    public string? PropertyPath { get; init; }

    /// <summary>
    /// Warning code for programmatic handling.
    /// </summary>
    public string? Code { get; init; }
}
