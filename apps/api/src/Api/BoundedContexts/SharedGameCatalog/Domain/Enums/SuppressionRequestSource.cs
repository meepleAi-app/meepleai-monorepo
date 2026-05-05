namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Origin of a takedown request that triggered suppression of a MechanicAnalysis.
/// Recorded in mechanic_suppression_audit for T5 kill-switch accountability (ADR-051).
/// </summary>
public enum SuppressionRequestSource
{
    Email = 1,
    Legal = 2,
    Other = 3
}
