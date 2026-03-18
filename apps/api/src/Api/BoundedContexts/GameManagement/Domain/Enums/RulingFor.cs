namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// Indicates which party the AI arbitrator's ruling favors.
/// </summary>
internal enum RulingFor
{
    /// <summary>The ruling favors the player who initiated the dispute.</summary>
    Initiator = 0,

    /// <summary>The ruling favors the responding player.</summary>
    Respondent = 1,

    /// <summary>The rule is ambiguous; no clear ruling for either side.</summary>
    Ambiguous = 2
}
