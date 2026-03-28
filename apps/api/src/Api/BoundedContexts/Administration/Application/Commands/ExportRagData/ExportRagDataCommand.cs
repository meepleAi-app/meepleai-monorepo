using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.ExportRagData;

/// <summary>
/// Command to export the full RAG dataset (vector documents, text chunks, and embeddings)
/// to a versioned snapshot bundle for production go-live or disaster recovery.
/// </summary>
internal sealed record ExportRagDataCommand : IRequest<ExportRagDataResult>
{
    /// <summary>
    /// When true, includes a copy of the source PDF binary in the snapshot bundle.
    /// </summary>
    public bool IncludeSourcePdf { get; init; }

    /// <summary>
    /// When true, counts what would be exported without writing any files.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Optional GUID filter to export only documents for a specific game.
    /// When null, all completed vector documents are exported.
    /// </summary>
    public string? GameIdFilter { get; init; }
}

/// <summary>
/// Result returned after a full RAG data export (or dry run).
/// </summary>
internal sealed record ExportRagDataResult(
    int TotalDocuments,
    int TotalChunks,
    int TotalEmbeddings,
    int Skipped,
    int Failed,
    bool IsDryRun,
    string SnapshotId,
    List<string> Errors);
