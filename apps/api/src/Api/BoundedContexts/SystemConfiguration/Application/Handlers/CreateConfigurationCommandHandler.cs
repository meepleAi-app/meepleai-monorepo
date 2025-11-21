using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

public class CreateConfigurationCommandHandler : ICommandHandler<CreateConfigurationCommand, ConfigurationDto>
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateConfigurationCommandHandler(
        IConfigurationRepository configurationRepository,
        IUnitOfWork unitOfWork)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ConfigurationDto> Handle(CreateConfigurationCommand command, CancellationToken cancellationToken)
    {
        var key = new ConfigKey(command.Key);

        var config = new Domain.Entities.SystemConfiguration(
            id: Guid.NewGuid(),
            key: key,
            value: command.Value,
            valueType: command.ValueType,
            createdByUserId: command.CreatedByUserId,
            description: command.Description,
            category: command.Category,
            environment: command.Environment,
            requiresRestart: command.RequiresRestart
        );

        await _configurationRepository.AddAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return MapToDto(config);
    }

    private static ConfigurationDto MapToDto(Domain.Entities.SystemConfiguration config)
    {
        return new ConfigurationDto(
            Id: config.Id,
            Key: config.Key.Value,
            Value: config.Value,
            ValueType: config.ValueType,
            Description: config.Description,
            Category: config.Category,
            IsActive: config.IsActive,
            RequiresRestart: config.RequiresRestart,
            Environment: config.Environment,
            Version: config.Version,
            CreatedAt: config.CreatedAt,
            UpdatedAt: config.UpdatedAt
        );
    }
}
