namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Lifecycle status of a live game session.
/// </summary>
public enum LiveSessionStatus
{
    /// <summary>
    /// Session has been created but not yet configured.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Session is being set up (players joining, configuring toolkit).
    /// </summary>
    Setup = 1,

    /// <summary>
    /// Session is actively in progress.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Session has been temporarily paused.
    /// </summary>
    Paused = 3,

    /// <summary>
    /// Session has been completed.
    /// </summary>
    Completed = 4
}
