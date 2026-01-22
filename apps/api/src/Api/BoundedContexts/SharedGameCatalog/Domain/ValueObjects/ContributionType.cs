namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Represents the type of contribution in a share request.
/// </summary>
public enum ContributionType
{
    /// <summary>
    /// Contributing a new game that doesn't exist in the shared catalog.
    /// Will create a new SharedGame entry upon approval.
    /// </summary>
    NewGame = 0,

    /// <summary>
    /// Contributing additional content to an existing game in the shared catalog.
    /// Will enhance existing SharedGame entry upon approval.
    /// </summary>
    AdditionalContent = 1
}
