using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.ImportRagData;

/// <summary>
/// Imports RAG data from a backup snapshot into the database.
/// Used during production migration to restore vector documents, text chunks, and embeddings.
/// </summary>
internal sealed record ImportRagDataCommand : IRequest<ImportRagDataResult>
{
    /// <summary>The snapshot path (e.g. "rag-exports/2026-03-28T12-00-00Z") to import from.</summary>
    public string SnapshotPath { get; init; } = string.Empty;

    /// <summary>
    /// When true, skips importing stored embeddings and marks documents for re-embedding.
    /// Useful when migrating to a different embedding model.
    /// </summary>
    public bool ReEmbed { get; init; }
}

/// <summary>Result of a RAG data import operation.</summary>
internal sealed record ImportRagDataResult(
    int TotalDocuments,
    int Imported,
    int Skipped,
    int Failed,
    int ReEmbedded,
    List<string> Warnings,
    List<string> Errors);
