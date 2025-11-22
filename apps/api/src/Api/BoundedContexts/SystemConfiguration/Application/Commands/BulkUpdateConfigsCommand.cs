using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to bulk update multiple configurations atomically.
/// </summary>
public record BulkUpdateConfigsCommand(
    IReadOnlyList<ConfigurationUpdate> Updates,
    Guid UserId
) : ICommand<IReadOnlyList<ConfigurationDto>>;

/// <summary>
/// Single configuration update in bulk operation.
/// </summary>
public record ConfigurationUpdate(
    Guid Id,
    string Value
);
