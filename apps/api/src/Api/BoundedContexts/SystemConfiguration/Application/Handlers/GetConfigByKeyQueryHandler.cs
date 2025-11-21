using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Handles retrieval of configuration by key.
/// Returns null if configuration not found.
/// </summary>
public class GetConfigByKeyQueryHandler : IQueryHandler<GetConfigByKeyQuery, ConfigurationDto?>
{
    private readonly IConfigurationRepository _configurationRepository;

    public GetConfigByKeyQueryHandler(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
    }

    public async Task<ConfigurationDto?> Handle(GetConfigByKeyQuery query, CancellationToken cancellationToken)
    {
        var config = await _configurationRepository.GetByKeyAsync(
            query.Key,
            query.Environment,
            query.ActiveOnly,
            cancellationToken);

        if (config == null)
            return null;

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
