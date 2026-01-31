using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertConfiguration;

/// <summary>
/// Query to get alert configuration by category (Issue #915)
/// </summary>
internal record GetAlertConfigurationQuery(string Category) : IRequest<AlertConfigurationDto>;

/// <summary>
/// DTO for alert configuration (Issue #915)
/// </summary>
internal record AlertConfigurationDto(
    Guid Id,
    string ConfigKey,
    string ConfigValue,
    string Category,
    bool IsEncrypted,
    string? Description,
    DateTime UpdatedAt,
    string UpdatedBy);
