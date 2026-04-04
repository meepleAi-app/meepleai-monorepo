using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;

/// <summary>
/// Command to export a single PDF document's RAG bundle (chunks + embeddings) to the "latest"
/// snapshot. Called automatically by <c>RagBackupOnIndexedEventHandler</c> after indexing.
/// </summary>
internal sealed record IncrementalRagBackupCommand(Guid PdfDocumentId) : IRequest<IncrementalRagBackupResult>;

/// <summary>
/// Result of an incremental RAG backup operation.
/// </summary>
internal sealed record IncrementalRagBackupResult(bool Success, string? Error = null);
