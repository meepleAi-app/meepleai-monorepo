using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

public class ToggleConfigurationCommandHandler : ICommandHandler<ToggleConfigurationCommand, ConfigurationDto>
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ToggleConfigurationCommandHandler(
        IConfigurationRepository configurationRepository,
        IUnitOfWork unitOfWork)
    {
        _configurationRepository = configurationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ConfigurationDto> Handle(ToggleConfigurationCommand command, CancellationToken cancellationToken)
    {
        var config = await _configurationRepository.GetByIdAsync(command.ConfigId, cancellationToken);

        if (config == null)
            throw new DomainException($"Configuration with ID {command.ConfigId} not found");

        if (command.IsActive)
            config.Activate();
        else
            config.Deactivate();

        await _configurationRepository.UpdateAsync(config, cancellationToken);
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
