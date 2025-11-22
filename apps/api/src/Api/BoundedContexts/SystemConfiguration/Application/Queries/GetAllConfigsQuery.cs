using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

public record GetAllConfigsQuery(
    string? Category = null,
    string? Environment = null,
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = 50
) : IQuery<PagedConfigurationResult>;

/// <summary>
/// Paginated result wrapper for configurations.
/// </summary>
public record PagedConfigurationResult(
    IReadOnlyList<ConfigurationDto> Items,
    int Total,
    int Page,
    int PageSize
);
