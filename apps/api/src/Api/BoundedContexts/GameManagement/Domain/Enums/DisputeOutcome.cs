namespace Api.BoundedContexts.GameManagement.Domain.Enums;

/// <summary>
/// The final outcome of a structured rule dispute after voting.
/// </summary>
internal enum DisputeOutcome
{
    /// <summary>Dispute is open; no verdict or votes tallied yet.</summary>
    Pending = 0,

    /// <summary>Players accepted the AI verdict by majority vote.</summary>
    VerdictAccepted = 1,

    /// <summary>Players overrode the AI verdict by majority vote.</summary>
    VerdictOverridden = 2
}
