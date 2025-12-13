using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles configuration value validation.
/// Delegates to ConfigurationValidator domain service.
/// </summary>
public class ValidateConfigCommandHandler : ICommandHandler<ValidateConfigCommand, ConfigurationValidationResult>
{
    // Note: Using static Validate method from ConfigurationValidator
    // No instance fields needed

    public Task<ConfigurationValidationResult> Handle(ValidateConfigCommand command, CancellationToken cancellationToken)
    {
        var domainResult = ConfigurationValidator.Validate(command.Key, command.Value, command.ValueType);

        var result = new ConfigurationValidationResult(
            IsValid: domainResult.IsValid,
            Errors: domainResult.Errors
        );

        return Task.FromResult(result);
    }
}
