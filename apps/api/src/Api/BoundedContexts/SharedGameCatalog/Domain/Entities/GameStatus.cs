namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Represents the publication status of a shared game in the catalog.
/// Issue #2514: Three-state workflow with approval
/// </summary>
public enum GameStatus
{
    /// <summary>
    /// Game is in draft state (DaCompletare), not visible to public users.
    /// Only admins and editors can see and edit draft games.
    /// Can transition to: PendingApproval
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Game is pending approval (InAutorizzazione) after submission.
    /// Waiting for admin approval before becoming published.
    /// Can transition to: Published (approved) or Draft (rejected)
    /// </summary>
    PendingApproval = 1,

    /// <summary>
    /// Game is published (Disponibile) and visible to all users.
    /// This is the normal state for games in the shared catalog.
    /// Can transition to: Archived
    /// </summary>
    Published = 2,

    /// <summary>
    /// Game is archived and no longer visible to public users.
    /// Archived games can be restored by admins.
    /// Can transition to: Draft (for re-editing)
    /// </summary>
    Archived = 3
}
