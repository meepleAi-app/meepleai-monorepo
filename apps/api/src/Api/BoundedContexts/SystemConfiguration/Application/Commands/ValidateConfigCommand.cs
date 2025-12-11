using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to validate configuration value before persisting.
/// Returns validation result with errors if any.
/// </summary>
public record ValidateConfigCommand(
    string Key,
    string Value,
    string ValueType
) : ICommand<ConfigurationValidationResult>;

/// <summary>
/// Result of configuration validation.
/// </summary>
public record ConfigurationValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors
);
