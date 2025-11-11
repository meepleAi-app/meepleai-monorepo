using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles import of configurations from backup/export.
/// Supports both creation and overwrite modes.
/// </summary>
public class ImportConfigsCommandHandler : ICommandHandler<ImportConfigsCommand, int>
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportConfigsCommandHandler(
        IConfigurationRepository configurationRepository,
        IUnitOfWork unitOfWork)
    {
        _configurationRepository = configurationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(ImportConfigsCommand command, CancellationToken cancellationToken)
    {
        var importedCount = 0;

        foreach (var item in command.Configurations)
        {
            var key = new ConfigKey(item.Key);
            var existing = await _configurationRepository.GetByKeyAsync(key.Value, cancellationToken);

            if (existing != null)
            {
                if (command.OverwriteExisting)
                {
                    // Update existing configuration
                    existing.UpdateValue(item.Value, command.UserId);
                    await _configurationRepository.UpdateAsync(existing, cancellationToken);
                    importedCount++;
                }
                // If not overwriting, skip this configuration
            }
            else
            {
                // Create new configuration
                var config = new Domain.Entities.SystemConfiguration(
                    id: Guid.NewGuid(),
                    key: key,
                    value: item.Value,
                    valueType: item.ValueType,
                    createdByUserId: command.UserId,
                    description: item.Description,
                    category: item.Category,
                    environment: item.Environment,
                    requiresRestart: item.RequiresRestart
                );

                if (!item.IsActive)
                {
                    config.Deactivate();
                }

                await _configurationRepository.AddAsync(config, cancellationToken);
                importedCount++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return importedCount;
    }
}
