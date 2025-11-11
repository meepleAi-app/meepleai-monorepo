using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to import multiple configurations from backup/export.
/// </summary>
public record ImportConfigsCommand(
    IReadOnlyList<ConfigurationImportItem> Configurations,
    bool OverwriteExisting,
    Guid UserId
) : ICommand<int>;

/// <summary>
/// Single configuration in import operation.
/// </summary>
public record ConfigurationImportItem(
    string Key,
    string Value,
    string ValueType,
    string? Description,
    string Category,
    bool IsActive,
    bool RequiresRestart,
    string Environment
);
