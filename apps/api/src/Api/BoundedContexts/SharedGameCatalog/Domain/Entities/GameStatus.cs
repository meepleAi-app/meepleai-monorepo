namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Represents the publication status of a shared game in the catalog.
/// </summary>
public enum GameStatus
{
    /// <summary>
    /// Game is in draft state, not visible to public users.
    /// Only admins and editors can see and edit draft games.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Game is published and visible to all users.
    /// This is the normal state for games in the shared catalog.
    /// </summary>
    Published = 1,

    /// <summary>
    /// Game is archived and no longer visible to public users.
    /// Archived games can be restored by admins.
    /// </summary>
    Archived = 2
}
