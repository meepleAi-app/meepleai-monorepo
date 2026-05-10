using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to re-embed all chunks of all PDFs belonging to a game's Knowledge Base.
///
/// Implementation note: This is a synchronous implementation returning a completed status
/// immediately. Background-job queuing is a planned future enhancement.
///
/// Issue #903: SG2 — KB lifecycle con re-index smoke test.
/// </summary>
/// <param name="GameId">The ID of the game whose KB should be re-indexed.</param>
/// <param name="UserId">The ID of the user triggering the re-index (for authorization).</param>
internal record ReindexGameKbCommand(Guid GameId, Guid UserId)
    : ICommand<KbJobResponse>;

/// <summary>
/// Response DTO for async KB job operations (reindex, RAPTOR rebuild).
/// </summary>
/// <param name="JobId">A stable identifier for this job invocation (for future polling).</param>
/// <param name="Status">
/// Current status of the job.
/// "completed" = finished synchronously.
/// "queued" = accepted into background queue (future async implementation).
/// </param>
/// <param name="PdfCount">Number of PDF documents found for the game (reindex only).</param>
public record KbJobResponse(Guid JobId, string Status, int? PdfCount = null);
