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
) : ICommand<ImportConfigsResult>;

/// <summary>
/// Esito dell'import: quante righe sono state create o aggiornate, e quali chiavi sono state saltate perché
/// riservate (#4612). Una chiave saltata non è un errore: un export fatto prima della correzione la conteneva.
/// </summary>
internal record ImportConfigsResult(int ImportedCount, IReadOnlyList<string> SkippedKeys);

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
