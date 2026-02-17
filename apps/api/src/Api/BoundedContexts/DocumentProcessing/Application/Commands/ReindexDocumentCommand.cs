using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to reindex a PDF document (delete vectors/chunks, reset to Pending, re-trigger pipeline).
/// PDF Storage Management Hub: Phase 5.
/// </summary>
internal record ReindexDocumentCommand(Guid PdfId) : ICommand;
