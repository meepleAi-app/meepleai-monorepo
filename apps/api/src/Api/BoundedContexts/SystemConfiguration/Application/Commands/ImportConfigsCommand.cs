using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Command to import multiple configurations from backup/export.
/// </summary>
internal record ImportConfigsCommand(
    IReadOnlyList<ConfigurationImportItem> Configurations,
    bool OverwriteExisting,
    Guid UserId
) : ICommand<int>;

/// <summary>
/// Single configuration in import operation.
/// </summary>
internal record ConfigurationImportItem(
    string Key,
    string Value,
    string ValueType,
    string? Description,
    string Category,
    bool IsActive,
    bool RequiresRestart,
    string Environment
);
