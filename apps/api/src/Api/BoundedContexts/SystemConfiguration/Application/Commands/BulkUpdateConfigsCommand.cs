using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
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
