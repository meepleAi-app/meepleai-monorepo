namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// ME-M1.3 #2494 AC-5 — distinguishes the semantic origin of a
/// <see cref="Events.MechanicAnalysisCostCapOverriddenEvent"/>.
///
/// Today the event is raised by the admin override path (default cause)
/// AND by the pipeline mid-stream overrun path (explicit cause). Future
/// causes (e.g. retry-budget exhaustion) can extend this enum without
/// breaking the event consumer contract.
/// </summary>
internal enum CostCapOverrunCause
{
    /// <summary>
    /// Admin explicitly raised the cap beyond the default via
    /// <see cref="Aggregates.MechanicAnalysis.OverrideCostCap"/>. Default,
    /// preserves the event semantic in place before AC-5.
    /// </summary>
    AdminOverride = 0,

    /// <summary>
    /// Pipeline detected cumulative cost &gt; effective cap AFTER at least one
    /// section completed successfully. The aggregate is salvaged as
    /// <see cref="MechanicAnalysisStatus.PartiallyExtracted"/> rather than
    /// fully aborted.
    /// </summary>
    MidStreamOverrun = 1
}
