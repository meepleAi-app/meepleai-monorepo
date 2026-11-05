namespace Api.BoundedContexts.KnowledgeBase.Domain.Chunking;

/// <summary>
/// The page of a chunk, as the indexing models carry it and as it is stored (#4782).
/// </summary>
/// <remarks>
/// In the indexing models (<c>DocumentSection.Page</c>, <c>ChunkMetadata.Page</c>, <c>DocumentChunk.Page</c>) a page is
/// an <c>int</c> and <see cref="Unknown"/> (0) means that nobody measured it, as it always meant for a chunk whose
/// extractor gave no page. In <c>text_chunks.PageNumber</c> it is an <c>int?</c>, and «unknown» is <c>NULL</c>, never
/// <c>0</c> (the contract of #4827): a 0 stored in the column would be read as a page by whoever does not know the
/// convention, and a 1 is the false page this type exists to stop writing. The vector rows
/// (<c>pgvector_embeddings.page_number</c>) have no «unknown» and are clamped to 1 on write, as they always were.
/// </remarks>
internal static class ChunkPage
{
    /// <summary>The page of a chunk whose page nobody measured.</summary>
    public const int Unknown = 0;

    /// <summary>The value for <c>text_chunks.PageNumber</c>: the page itself, or <c>null</c> when there is none.</summary>
    public static int? ForStorage(int page) => page > 0 ? page : null;
}
