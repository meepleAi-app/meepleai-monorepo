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
internal class ImportConfigsCommandHandler : ICommandHandler<ImportConfigsCommand, int>
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ImportConfigsCommandHandler(
        IConfigurationRepository configurationRepository,
        IUnitOfWork unitOfWork)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<int> Handle(ImportConfigsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Preload all existing configs by key in a single query to avoid N+1
        var keys = command.Configurations.Select(c => c.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existingConfigs = await _configurationRepository.GetByKeysAsync(keys, activeOnly: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        var configByKey = existingConfigs
            .GroupBy(c => c.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var importedCount = 0;

        foreach (var item in command.Configurations)
        {
            var key = new ConfigKey(item.Key);
            // Find existing config matching key and environment
            var existing = configByKey.TryGetValue(key.Value, out var matches)
                ? matches.FirstOrDefault(c =>
                    string.Equals(c.Environment, item.Environment, StringComparison.OrdinalIgnoreCase))
                : null;

            if (existing != null)
            {
                if (command.OverwriteExisting)
                {
                    existing.UpdateValue(item.Value, command.UserId);
                    await _configurationRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
                    importedCount++;
                }
            }
            else
            {
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

                await _configurationRepository.AddAsync(config, cancellationToken).ConfigureAwait(false);
                importedCount++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return importedCount;
    }
}
