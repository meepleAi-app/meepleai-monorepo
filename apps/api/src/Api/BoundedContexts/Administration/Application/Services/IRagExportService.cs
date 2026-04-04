using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Orchestrates writing a RAG snapshot bundle to storage.
/// One bundle = one VectorDocument (one PDF's worth of chunks + embeddings).
/// </summary>
internal interface IRagExportService
{
    /// <summary>
    /// Writes the complete bundle for a single document:
    /// metadata.json, chunks.jsonl, and embeddings.jsonl under
    /// <c>{snapshotId}/{gameSlug}/{pdfDocumentId}/</c>.
    /// </summary>
    Task ExportDocumentBundleAsync(
        string snapshotId,
        VectorDocumentEntity vectorDoc,
        PdfDocumentEntity pdfDoc,
        string gameName,
        List<TextChunkEntity> chunks,
        List<PgVectorEmbeddingEntity> embeddings,
        CancellationToken ct = default);

    /// <summary>Writes the manifest.json file at the snapshot root.</summary>
    Task WriteManifestAsync(
        string snapshotId,
        RagExportManifest manifest,
        CancellationToken ct = default);

    /// <summary>
    /// Reads and deserialises the manifest.json file at the snapshot root.
    /// Returns <c>null</c> when the file does not exist.
    /// </summary>
    Task<RagExportManifest?> ReadManifestAsync(
        string snapshotId,
        CancellationToken ct = default);
}

/// <summary>
/// Static helpers for serialising RAG export objects to JSON / JSONL bytes.
/// Used by both the export service implementation and tests.
/// </summary>
internal static class RagExportSerializer
{
    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Serialises a sequence of items as JSONL (one compact JSON object per line,
    /// lines separated by <c>\n</c>).
    /// </summary>
    public static byte[] ToJsonlBytes<T>(IEnumerable<T> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            sb.Append(JsonSerializer.Serialize(item, CompactOptions));
            sb.Append('\n');
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Serialises a single item as pretty-printed JSON bytes.
    /// </summary>
    public static byte[] ToJsonBytes<T>(T item) =>
        JsonSerializer.SerializeToUtf8Bytes(item, PrettyOptions);
}
