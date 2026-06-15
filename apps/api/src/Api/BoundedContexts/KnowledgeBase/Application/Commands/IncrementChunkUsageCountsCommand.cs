using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// #2311 BE-1 — fire-and-forget increment of <c>text_chunks.usage_count</c> for the chunks
/// cited by a freshly persisted assistant message. Invoked from
/// <see cref="ChatWithSessionAgentCommandHandler"/> after <c>SaveChangesAsync</c> so the
/// counter mirrors what survives to durable storage (transient retrieval-only chunks are
/// never counted).
///
/// Each (PdfDocumentId, ChunkIndex) locator is incremented at most once per command
/// invocation — duplicate citations within the same assistant message are deduplicated by
/// the handler. Cross-message duplication within the same thread is intentionally NOT
/// deduplicated in BE-1 (DEC-D2 downgraded from distinct-thread to distinct-message scope;
/// upgrading to true distinct-thread requires a junction table — tracked separately).
/// </summary>
internal sealed record IncrementChunkUsageCountsCommand(
    IReadOnlyList<ChunkUsageLocator> Locators
) : ICommand<int>;

/// <summary>
/// Identifies a TextChunk by its natural composite key (PdfDocumentId, ChunkIndex).
/// Matches the <c>ix_text_chunks_pdf_chunk_index</c> unique index on the EF entity.
/// </summary>
internal sealed record ChunkUsageLocator(Guid PdfDocumentId, int ChunkIndex);
