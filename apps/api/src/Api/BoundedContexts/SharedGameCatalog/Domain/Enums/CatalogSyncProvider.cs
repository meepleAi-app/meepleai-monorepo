namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Provider of a <see cref="Aggregates.CatalogSyncRun"/> — identifies the source pipeline
/// that produced the run (#1861).
/// </summary>
public enum CatalogSyncProvider
{
    /// <summary>Automatic BGG (BoardGameGeek) API sync, typically cron-triggered.</summary>
    BggApi = 0,

    /// <summary>Manual Excel/CSV bulk import via admin UI.</summary>
    CsvImport = 1,

    /// <summary>One-off manual entry (e.g. assign BGG id to skeleton).</summary>
    Manual = 2
}
