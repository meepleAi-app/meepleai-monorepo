namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Status of a play record in its lifecycle.
/// </summary>
internal enum PlayRecordStatus
{
    /// <summary>
    /// Session is planned but not yet started.
    /// </summary>
    Planned = 0,

    /// <summary>
    /// Session is currently in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Session has been completed.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Session has been archived (completed and archived for history).
    /// </summary>
    Archived = 3
}
