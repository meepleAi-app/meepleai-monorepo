using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to export configurations for backup/migration.
/// </summary>
public record ExportConfigsQuery(
    string Environment,
    bool ActiveOnly = true
) : IQuery<ConfigurationExportDto>;

/// <summary>
/// Result of configuration export operation.
/// </summary>
public record ConfigurationExportDto(
    IReadOnlyList<ConfigurationDto> Configurations,
    DateTime ExportedAt,
    string Environment
);
