// Issue #2248 (epic #2242): Silent failure detector for the PDF→KB flag pipeline.
// Counter is incremented by KbFlagDriftAuditJob when a row violates the invariant
// "PdfDocument.ProcessingState == Ready ⇒ SharedGame.HasKnowledgeBase == true"
// (the same root-cause #2243 Block A guards against at the publish-event level).
//
// SLO = 0. Any nonzero increment is a P1: the tactical Block A publish was bypassed
// or a new ingestion path was introduced without raising VectorDocumentIndexedEvent.
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Total number of (PdfDocument, SharedGame) pairs detected by the periodic
    /// audit where the PDF reached the Ready terminal state but the SharedGame's
    /// HasKnowledgeBase flag is still false — proof that
    /// VectorDocumentIndexedEvent was never published for that document.
    /// </summary>
    /// <remarks>
    /// Cardinality: zero tags. Per-game labels were considered but rejected
    /// (catalog can hold tens of thousands of SharedGames; an alert on the
    /// aggregate counter is sufficient — operators inspect the audit job's
    /// structured log for the offending IDs).
    /// </remarks>
    public static readonly Counter<long> PdfIndexedNoKbFlagTotal = Meter.CreateCounter<long>(
        name: "meepleai.pdf.indexed.no.kb.flag.total",
        unit: "rows",
        description:
            "Audit counter: PdfDocument.ProcessingState=Ready AND SharedGame.HasKnowledgeBase=false. " +
            "SLO=0; any increment indicates the publish-event chain is broken (#2243 / #2244).");

    /// <summary>
    /// Records one drifted row found by KbFlagDriftAuditJob.
    /// </summary>
    public static void RecordPdfIndexedNoKbFlagDrift()
    {
        PdfIndexedNoKbFlagTotal.Add(1);
    }
}
