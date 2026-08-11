using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;

/// <summary>
/// Bulk-triggers re-index of ALL <c>Ready</c> PDFs whose <c>IndexerVersion</c> differs from the
/// target (coordinate-aware v1.2), by fanning out the existing <see cref="ReindexDocumentCommand"/>.
/// SP3 RAG bulk re-index (issue #3269, epic #3266); target bumped by #3409 (SP-E, epic #3403).
/// </summary>
/// <param name="RequestedBy">The admin user id that triggered the bulk re-index.</param>
/// <param name="TargetVersion">
/// Versione target da applicare a tutte le righe. <c>null</c> = usa
/// <c>IndexerVersionRegistry.Current.Version</c>.
/// </param>
internal sealed record BulkReindexReadyCommand(Guid RequestedBy, string? TargetVersion = null)
    : ICommand<BulkReindexResult>;
