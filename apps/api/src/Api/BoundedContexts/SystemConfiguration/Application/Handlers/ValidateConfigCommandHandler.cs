using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.SharedKernel.Application.Interfaces;
using System.Text.Json;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles configuration value validation.
/// Validates type compatibility and value format.
/// </summary>
public class ValidateConfigCommandHandler : ICommandHandler<ValidateConfigCommand, ConfigurationValidationResult>
{
    public Task<ConfigurationValidationResult> Handle(ValidateConfigCommand command, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        // Validate based on value type
        switch (command.ValueType.ToLowerInvariant())
        {
            case "string":
                // All values are valid as strings
                break;

            case "int":
                if (!int.TryParse(command.Value, out _))
                {
                    errors.Add($"Value '{command.Value}' is not a valid integer");
                }
                break;

            case "long":
                if (!long.TryParse(command.Value, out _))
                {
                    errors.Add($"Value '{command.Value}' is not a valid long integer");
                }
                break;

            case "double":
            case "decimal":
                if (!double.TryParse(command.Value, out _))
                {
                    errors.Add($"Value '{command.Value}' is not a valid decimal number");
                }
                break;

            case "bool":
            case "boolean":
                if (!bool.TryParse(command.Value, out _))
                {
                    errors.Add($"Value '{command.Value}' is not a valid boolean (true/false)");
                }
                break;

            case "json":
                try
                {
                    JsonDocument.Parse(command.Value);
                }
                catch (JsonException ex)
                {
                    errors.Add($"Value is not valid JSON: {ex.Message}");
                }
                break;

            default:
                errors.Add($"Unknown value type: {command.ValueType}");
                break;
        }

        // Key-specific validations
        ValidateKeySpecificRules(command.Key, command.Value, errors);

        var result = new ConfigurationValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors.AsReadOnly()
        );

        return Task.FromResult(result);
    }

    private static void ValidateKeySpecificRules(string key, string value, List<string> errors)
    {
        // Rate limit validations
        if (key.Contains("RateLimit") && key.Contains("MaxTokens"))
        {
            if (int.TryParse(value, out var maxTokens) && maxTokens < 0)
            {
                errors.Add("MaxTokens must be non-negative");
            }
        }

        if (key.Contains("RateLimit") && key.Contains("RefillRate"))
        {
            if (int.TryParse(value, out var refillRate) && refillRate < 0)
            {
                errors.Add("RefillRate must be non-negative");
            }
        }

        // AI/LLM validations
        if (key.Contains("AI:Temperature"))
        {
            if (double.TryParse(value, out var temp) && (temp < 0 || temp > 2))
            {
                errors.Add("Temperature must be between 0 and 2");
            }
        }

        if (key.Contains("AI:MaxTokens"))
        {
            if (int.TryParse(value, out var maxTokens) && maxTokens < 1)
            {
                errors.Add("MaxTokens must be positive");
            }
        }

        // RAG validations
        if (key.Contains("Rag:TopK"))
        {
            if (int.TryParse(value, out var topK) && topK < 1)
            {
                errors.Add("TopK must be at least 1");
            }
        }

        if (key.Contains("Rag:MinScore"))
        {
            if (double.TryParse(value, out var minScore) && (minScore < 0 || minScore > 1))
            {
                errors.Add("MinScore must be between 0 and 1");
            }
        }
    }
}
