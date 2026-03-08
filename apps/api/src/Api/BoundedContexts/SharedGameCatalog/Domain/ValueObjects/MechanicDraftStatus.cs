namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Status of a mechanic extraction draft.
/// </summary>
public enum MechanicDraftStatus
{
    /// <summary>
    /// Draft is in progress, human is working on notes/AI assists.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// All sections are complete, ready for finalization.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Draft has been finalized into a RulebookAnalysis entry.
    /// </summary>
    Activated = 2
}
