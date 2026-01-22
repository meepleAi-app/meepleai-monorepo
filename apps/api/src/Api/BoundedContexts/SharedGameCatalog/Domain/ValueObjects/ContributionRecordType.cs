namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Represents the type of contribution recorded in the contributor history.
/// Used for tracking what kind of contributions users have made to shared games.
/// </summary>
public enum ContributionRecordType
{
    /// <summary>
    /// Initial game submission that created the shared game entry.
    /// Only one per game, assigned to the primary contributor.
    /// </summary>
    InitialSubmission = 0,

    /// <summary>
    /// Added new documents (rulebooks, player aids, etc.) to the game.
    /// </summary>
    DocumentAddition = 1,

    /// <summary>
    /// Updated game metadata (description, player count, playing time, etc.).
    /// </summary>
    MetadataUpdate = 2,

    /// <summary>
    /// General content enhancement or improvement.
    /// </summary>
    ContentEnhancement = 3
}
