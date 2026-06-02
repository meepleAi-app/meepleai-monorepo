namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to DocumentProcessing BC: read-only view of PDF doc + its chunks needed for eval.
/// </summary>
public interface IPdfDocumentReadModel
{
    Task<PdfDocSnapshot?> GetSnapshotAsync(Guid docId, CancellationToken ct);
}

public sealed record PdfDocSnapshot(
    Guid Id,
    string FileName,
    int ChunkCount,
    string ProcessingState,
    IReadOnlyList<ChunkSnapshot> TopChunks);

public sealed record ChunkSnapshot(Guid ChunkId, int Position, string Snippet);
