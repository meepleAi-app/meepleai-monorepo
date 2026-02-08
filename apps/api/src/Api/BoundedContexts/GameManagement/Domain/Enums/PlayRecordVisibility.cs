namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Visibility level for play records.
/// </summary>
public enum PlayRecordVisibility
{
    /// <summary>
    /// Only the creator can view the record.
    /// </summary>
    Private = 0,

    /// <summary>
    /// All members of the specified group can view the record.
    /// </summary>
    Group = 1
}
