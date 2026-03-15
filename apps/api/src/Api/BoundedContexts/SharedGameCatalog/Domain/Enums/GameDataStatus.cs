namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Tracks the data completeness lifecycle of a shared game.
/// Separate from GameStatus (publication lifecycle).
/// A game cannot be submitted for approval unless GameDataStatus >= Enriched.
/// </summary>
public enum GameDataStatus
{
    /// <summary>
    /// Game created from Excel import with only title and optional BggId.
    /// Can transition to: EnrichmentQueued
    /// </summary>
    Skeleton = 0,

    /// <summary>
    /// Queued for BGG enrichment processing.
    /// Can transition to: Enriching
    /// </summary>
    EnrichmentQueued = 1,

    /// <summary>
    /// Currently being enriched with BGG data.
    /// Can transition to: Enriched, Failed
    /// </summary>
    Enriching = 2,

    /// <summary>
    /// Successfully enriched with BGG data. PDF may still be pending.
    /// Can transition to: PdfDownloading, Complete
    /// </summary>
    Enriched = 3,

    /// <summary>
    /// PDF rulebook is being downloaded.
    /// Can transition to: Complete, Enriched (on failure)
    /// </summary>
    PdfDownloading = 4,

    /// <summary>
    /// All data and PDF are ready. Terminal state.
    /// </summary>
    Complete = 5,

    /// <summary>
    /// Enrichment or processing failed.
    /// Can transition to: EnrichmentQueued (re-enqueue)
    /// </summary>
    Failed = 6
}
