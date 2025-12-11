using System.Text.Json;
using System.Globalization;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.SystemConfiguration.Domain.Services;

/// <summary>
/// Domain service for validating configuration values.
/// Encapsulates validation rules for type checking and domain-specific constraints.
/// </summary>
public class ConfigurationValidator
{
    /// <summary>
    /// Validates a configuration value against its declared type and domain rules.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Value to validate</param>
    /// <param name="valueType">Expected value type (string, int, bool, json, etc.)</param>
    /// <returns>Validation result with any errors</returns>
    public ValidationResult Validate(string key, string value, string valueType)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty", nameof(key));

        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be empty", nameof(value));

        if (string.IsNullOrWhiteSpace(valueType))
            throw new ArgumentException("ValueType cannot be empty", nameof(valueType));

        var errors = new List<string>();

        // Type validation
        ValidateValueType(value, valueType, errors);

        // Domain-specific validation
        ValidateDomainRules(key, value, valueType, errors);

        return new ValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors.AsReadOnly()
        );
    }

    private static void ValidateValueType(string value, string valueType, List<string> errors)
    {
        switch (valueType.ToLowerInvariant())
        {
            case "string":
                // All values are valid as strings
                break;

            case "int":
            case "integer":
                if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    errors.Add($"Value '{value}' is not a valid integer");
                }
                break;

            case "long":
                if (!long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    errors.Add($"Value '{value}' is not a valid long integer");
                }
                break;

            case "double":
            case "float":
            case "decimal":
                if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    errors.Add($"Value '{value}' is not a valid decimal number");
                }
                break;

            case "bool":
            case "boolean":
                if (!bool.TryParse(value, out _))
                {
                    errors.Add($"Value '{value}' is not a valid boolean (true/false)");
                }
                break;

            case "json":
                try
                {
                    JsonDocument.Parse(value);
                }
                catch (JsonException ex)
                {
                    errors.Add($"Value is not valid JSON: {ex.Message}");
                }
                break;

            default:
                errors.Add($"Unknown value type: {valueType}");
                break;
        }
    }

    private static void ValidateDomainRules(string key, string value, string valueType, List<string> errors)
    {
        // Rate limit validations
        if (key.Contains("RateLimit", StringComparison.OrdinalIgnoreCase))
        {
            ValidateRateLimitRules(key, value, errors);
        }

        // AI/LLM validations
        if (key.Contains("AI:", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("LLM:", StringComparison.OrdinalIgnoreCase))
        {
            ValidateAiRules(key, value, errors);
        }

        // RAG validations
        if (key.Contains("Rag:", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("RAG:", StringComparison.OrdinalIgnoreCase))
        {
            ValidateRagRules(key, value, errors);
        }

        // PDF validations
        if (key.Contains("Pdf:", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("PDF:", StringComparison.OrdinalIgnoreCase))
        {
            ValidatePdfRules(key, value, errors);
        }

        // Feature flags validations
        if (key.StartsWith("Features.", StringComparison.OrdinalIgnoreCase))
        {
            ValidateFeatureFlagRules(key, value, errors);
        }
    }

    private static void ValidateRateLimitRules(string key, string value, List<string> errors)
    {
        if (key.Contains("MaxTokens", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, CultureInfo.InvariantCulture, out var maxTokens) && maxTokens < 0)
            {
                errors.Add("MaxTokens must be non-negative");
            }
        }

        if (key.Contains("RefillRate", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, CultureInfo.InvariantCulture, out var refillRate) && refillRate < 0)
            {
                errors.Add("RefillRate must be non-negative");
            }
        }

        if (key.Contains("WindowSeconds", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, CultureInfo.InvariantCulture, out var windowSeconds) && windowSeconds < 1)
            {
                errors.Add("WindowSeconds must be at least 1");
            }
        }
    }

    private static void ValidateAiRules(string key, string value, List<string> errors)
    {
        if (key.Contains("Temperature", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(value, CultureInfo.InvariantCulture, out var temp) && (temp < 0 || temp > 2))
            {
                errors.Add("Temperature must be between 0 and 2");
            }
        }

        if (key.Contains("MaxTokens", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, CultureInfo.InvariantCulture, out var maxTokens) && maxTokens < 1)
            {
                errors.Add("MaxTokens must be positive");
            }
        }

        if (key.Contains("TopP", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(value, CultureInfo.InvariantCulture, out var topP) && (topP < 0 || topP > 1))
            {
                errors.Add("TopP must be between 0 and 1");
            }
        }

        if (key.Contains("FrequencyPenalty", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("PresencePenalty", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(value, CultureInfo.InvariantCulture, out var penalty) && (penalty < -2 || penalty > 2))
            {
                errors.Add("Penalty values must be between -2 and 2");
            }
        }
    }

    private static void ValidateRagRules(string key, string value, List<string> errors)
    {
        if (key.Contains("TopK", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, CultureInfo.InvariantCulture, out var topK) && topK < 1)
            {
                errors.Add("TopK must be at least 1");
            }
        }

        if (key.Contains("MinScore", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Threshold", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(value, CultureInfo.InvariantCulture, out var score) && (score < 0 || score > 1))
            {
                errors.Add("Score threshold must be between 0 and 1");
            }
        }

        if (key.Contains("MaxChunkSize", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, CultureInfo.InvariantCulture, out var chunkSize) && (chunkSize < 100 || chunkSize > 10000))
            {
                errors.Add("MaxChunkSize must be between 100 and 10000");
            }
        }

        if (key.Contains("ChunkOverlap", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, CultureInfo.InvariantCulture, out var overlap) && overlap < 0)
            {
                errors.Add("ChunkOverlap must be non-negative");
            }
        }
    }

    private static void ValidatePdfRules(string key, string value, List<string> errors)
    {
        if (key.Contains("MaxFileSizeMB", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, CultureInfo.InvariantCulture, out var maxSize) && maxSize < 1)
            {
                errors.Add("MaxFileSizeMB must be at least 1");
            }
        }

        if (key.Contains("TimeoutSeconds", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, CultureInfo.InvariantCulture, out var timeout) && timeout < 1)
            {
                errors.Add("TimeoutSeconds must be at least 1");
            }
        }

        if (key.Contains("Quality", StringComparison.OrdinalIgnoreCase) &&
            key.Contains("Threshold", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(value, CultureInfo.InvariantCulture, out var quality) && (quality < 0 || quality > 1))
            {
                errors.Add("Quality threshold must be between 0 and 1");
            }
        }
    }

    private static void ValidateFeatureFlagRules(string key, string value, List<string> errors)
    {
        // Feature flags must be boolean
        if (!bool.TryParse(value, out _))
        {
            errors.Add($"Feature flag '{key}' must be a boolean value (true/false)");
        }
    }
}

/// <summary>
/// Result of a configuration validation operation.
/// </summary>
/// <param name="IsValid">Whether the validation passed</param>
/// <param name="Errors">List of validation errors (empty if valid)</param>
public record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);