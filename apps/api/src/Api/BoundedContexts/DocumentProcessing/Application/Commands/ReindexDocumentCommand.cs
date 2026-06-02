using Api.BoundedContexts.Administration.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Command to reindex a PDF document (delete vectors/chunks, reset to Pending, re-trigger pipeline).
/// PDF Storage Management Hub: Phase 5. Issue #1673 estende con selettore versione indexer.
/// </summary>
/// <param name="PdfId">ID del documento PDF da re-indicizzare.</param>
/// <param name="IndexerVersion">
/// Versione pipeline da applicare. <c>null</c> = usa la versione storica del documento se
/// presente, altrimenti <c>IndexerVersionRegistry.Current.Version</c>.
/// </param>
[AuditableAction("DocumentReindex", "Document", Level = 2)]
internal sealed record ReindexDocumentCommand(Guid PdfId, string? IndexerVersion = null) : ICommand;
