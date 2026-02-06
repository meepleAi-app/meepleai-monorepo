namespace Api.BoundedContexts.UserLibrary.Domain.Enums;

/// <summary>
/// Represents the user's choice after a private game proposal is approved.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
public enum PostApprovalMigrationChoice
{
    /// <summary>
    /// User has not yet made a choice (default after approval).
    /// </summary>
    Pending = 0,

    /// <summary>
    /// User chooses to link their library entry to the new SharedGame and delete the PrivateGame.
    /// This updates the UserLibraryEntry to reference SharedGame and soft-deletes the PrivateGame.
    /// </summary>
    LinkToCatalog = 1,

    /// <summary>
    /// User chooses to keep the PrivateGame separate from the new SharedGame.
    /// No changes to UserLibraryEntry or PrivateGame.
    /// </summary>
    KeepPrivate = 2
}
