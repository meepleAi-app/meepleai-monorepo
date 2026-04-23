namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Per-claim review status inside a MechanicAnalysis.
/// A MechanicAnalysis transitions to Published only when all its claims are Approved (AC-10).
/// </summary>
public enum MechanicClaimStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
