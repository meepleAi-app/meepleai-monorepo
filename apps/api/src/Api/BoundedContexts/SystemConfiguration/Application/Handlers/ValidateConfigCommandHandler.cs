using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles configuration value validation.
/// Delegates to ConfigurationValidator domain service.
/// </summary>
internal class ValidateConfigCommandHandler : ICommandHandler<ValidateConfigCommand, ConfigurationValidationResult>
{
    private readonly ConfigurationValidator _validator;

    public ValidateConfigCommandHandler(ConfigurationValidator validator)
    {
        _validator = validator;
    }

    public Task<ConfigurationValidationResult> Handle(ValidateConfigCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var domainResult = _validator.Validate(command.Key, command.Value, command.ValueType);

        var result = new ConfigurationValidationResult(
            IsValid: domainResult.IsValid,
            Errors: domainResult.Errors
        );

        return Task.FromResult(result);
    }
}
