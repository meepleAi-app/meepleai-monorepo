using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

internal record GetAllConfigsQuery(
    string? Category = null,
    string? Environment = null,
    bool ActiveOnly = true,
    int Page = 1,
    int PageSize = 50
) : IQuery<PagedConfigurationResult>;

/// <summary>
/// Paginated result wrapper for configurations.
/// </summary>
internal record PagedConfigurationResult(
    IReadOnlyList<ConfigurationDto> Items,
    int Total,
    int Page,
    int PageSize
);
