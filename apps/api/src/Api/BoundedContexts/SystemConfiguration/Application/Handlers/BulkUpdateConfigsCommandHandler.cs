using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles bulk update of multiple configurations atomically.
/// </summary>
public class BulkUpdateConfigsCommandHandler : ICommandHandler<BulkUpdateConfigsCommand, IReadOnlyList<ConfigurationDto>>
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkUpdateConfigsCommandHandler(
        IConfigurationRepository configurationRepository,
        IUnitOfWork unitOfWork)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<IReadOnlyList<ConfigurationDto>> Handle(BulkUpdateConfigsCommand command, CancellationToken cancellationToken)
    {
        var updatedConfigs = new List<ConfigurationDto>();

        foreach (var update in command.Updates)
        {
            var config = await _configurationRepository.GetByIdAsync(update.Id, cancellationToken).ConfigureAwait(false);
            if (config == null)
            {
                throw new InvalidOperationException($"Configuration with ID {update.Id} not found");
            }

            config.UpdateValue(update.Value, command.UserId);
            await _configurationRepository.UpdateAsync(config, cancellationToken).ConfigureAwait(false);
            updatedConfigs.Add(MapToDto(config));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return updatedConfigs;
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
