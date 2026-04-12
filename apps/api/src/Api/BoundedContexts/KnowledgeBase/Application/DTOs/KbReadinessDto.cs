namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Knowledge Base readiness status for a game (Session Flow v2.1 — T3).
/// A KB is "ready" when at least one PDF is in the <c>Ready</c> terminal state AND a
/// <c>VectorDocumentEntity</c> exists with <c>IndexingStatus == "completed"</c>.
/// </summary>
/// <param name="IsReady">
/// <c>true</c> when an agent can be powered by this KB (at least one Ready PDF has a
/// completed vector index). Note that <see cref="State"/> may still carry warnings
/// (e.g. "PartiallyReady") even when this flag is <c>true</c>.
/// </param>
/// <param name="State">
/// High-level aggregate state. Possible values:
/// <list type="bullet">
///   <item><c>None</c> — game not found, or no PDFs uploaded.</item>
///   <item><c>Ready</c> — all ready PDFs indexed successfully.</item>
///   <item><c>PartiallyReady</c> — at least one ready PDF indexed, but some PDFs failed.</item>
///   <item><c>VectorPending</c> — PDFs reached Ready but vector index not yet completed.</item>
///   <item><c>Failed</c> — every PDF is in the Failed state.</item>
///   <item>One of the intermediate <c>PdfDocumentEntity.ProcessingState</c> values (e.g. <c>Extracting</c>, <c>Embedding</c>, <c>Indexing</c>).</item>
/// </list>
/// </param>
/// <param name="ReadyPdfCount">Number of PDFs whose <c>ProcessingState == "Ready"</c>.</param>
/// <param name="FailedPdfCount">Number of PDFs whose <c>ProcessingState == "Failed"</c>.</param>
/// <param name="Warnings">
/// Non-fatal advisories for the caller (e.g. "N PDF failed to index — answers may be incomplete").
/// Empty when there is nothing to warn about.
/// </param>
public record KbReadinessDto(
    bool IsReady,
    string State,
    int ReadyPdfCount,
    int FailedPdfCount,
    IReadOnlyList<string> Warnings);
