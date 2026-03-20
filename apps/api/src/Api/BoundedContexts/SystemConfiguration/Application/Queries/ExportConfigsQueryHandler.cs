using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Handles export of configurations for backup/migration.
/// </summary>
internal class ExportConfigsQueryHandler : IQueryHandler<ExportConfigsQuery, ConfigurationExportDto>
{
    private readonly IConfigurationRepository _configurationRepository;

    public ExportConfigsQueryHandler(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
    }

    public async Task<ConfigurationExportDto> Handle(ExportConfigsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        // Get all configurations for the environment
        var allConfigs = await _configurationRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Filter by environment and active status
        var configs = allConfigs
            .Where(c => string.Equals(c.Environment, query.Environment, StringComparison.Ordinal) || string.Equals(c.Environment, "All", StringComparison.Ordinal))
            .Where(c => !query.ActiveOnly || c.IsActive)
            .Select(MapToDto)
            .ToList();

        return new ConfigurationExportDto(
            Configurations: configs,
            ExportedAt: DateTime.UtcNow,
            Environment: query.Environment
        );
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
