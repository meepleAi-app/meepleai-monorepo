using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

public class GetAllConfigsQueryHandler : IQueryHandler<GetAllConfigsQuery, PagedConfigurationResult>
{
    private readonly IConfigurationRepository _configurationRepository;

    public GetAllConfigsQueryHandler(IConfigurationRepository configurationRepository)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
    }

    public async Task<PagedConfigurationResult> Handle(GetAllConfigsQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Domain.Entities.SystemConfiguration> configs;

        if (query.ActiveOnly)
        {
            configs = await _configurationRepository.GetActiveConfigurationsAsync(cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(query.Category))
        {
            configs = await _configurationRepository.GetByCategoryAsync(query.Category, cancellationToken);
        }
        else
        {
            configs = await _configurationRepository.GetAllAsync(cancellationToken);
        }

        // Filter by environment if specified
        if (!string.IsNullOrWhiteSpace(query.Environment))
        {
            configs = configs.Where(c => c.Environment == query.Environment || c.Environment == "All").ToList();
        }

        var total = configs.Count;
        var items = configs
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MapToDto)
            .ToList();

        return new PagedConfigurationResult(
            Items: items,
            Total: total,
            Page: query.Page,
            PageSize: query.PageSize
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
