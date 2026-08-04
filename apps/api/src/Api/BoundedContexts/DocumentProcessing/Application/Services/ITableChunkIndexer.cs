using System;
using System.Threading;
using System.Threading.Tasks;
using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #3435 SP4: persists an extracted table as a retrievable, citation-groundable RAG chunk
/// (a <c>text_chunks</c> row + a <c>pgvector_embeddings</c> row wired via <c>source_chunk_id</c>).
/// The region bbox is carried on the chunk's <c>bounding_boxes_json</c> so the citation draws the
/// table region; copyright tier is inherited from the parent PDF (Full-gated at answer time).
/// </summary>
public interface ITableChunkIndexer
{
    /// <summary>
    /// Upsert a table chunk for one image region. Idempotent by the deterministic chunk id derived
    /// from <paramref name="regionHash"/> (re-indexing replaces that chunk + its embedding, never the
    /// document's narrative chunks). Returns the text-chunk id, or <c>null</c> when the PDF has no
    /// game / vector document (not retrievable). Throws on a transient failure (embedding / index) so
    /// the caller can retry.
    /// </summary>
    Task<Guid?> IndexTableAsync(
        PdfDocumentEntity pdf,
        int pageNumber,
        double x,
        double y,
        double width,
        double height,
        string markdown,
        string regionHash,
        CancellationToken cancellationToken);
}
