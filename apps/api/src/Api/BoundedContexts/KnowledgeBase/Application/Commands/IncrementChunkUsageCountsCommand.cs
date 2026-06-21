using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// #2311 BE-1 + #2324 DEC-D2 — fire-and-forget increment of
/// <c>text_chunks.usage_count</c> for the chunks cited by a freshly persisted
/// assistant message. Invoked from <see cref="ChatWithSessionAgentCommandHandler"/>
/// after <c>SaveChangesAsync</c> so the counter mirrors what survives to durable
/// storage (transient retrieval-only chunks are never counted).
///
/// <para><b>Distinct-thread semantics (#2324)</b>: every (PdfDocumentId, ChunkIndex)
/// locator is incremented at most once <i>per distinct thread</i>. The handler
/// records each (ThreadId, MessageId, ChunkId) tuple in the
/// <c>chat_message_chunk_citations</c> junction table; when a subsequent message
/// in the same thread cites the same chunk, the EXISTS check on the junction
/// suppresses the increment. Two different threads citing the same chunk still
/// produce two increments. Intra-message duplicates (the same chunk cited
/// multiple times in a single assistant response) are deduplicated up-front in
/// the handler.</para>
///
/// <para><b>Idempotency</b>: replaying the same command (same
/// ThreadId+MessageId+chunks) is a no-op — the junction's composite PK rejects
/// the duplicate insert and the EXISTS check confirms the increment was already
/// taken in a prior run.</para>
/// </summary>
internal sealed record IncrementChunkUsageCountsCommand(
    Guid ThreadId,
    Guid MessageId,
    IReadOnlyList<ChunkUsageLocator> Locators
) : ICommand<int>;

/// <summary>
/// Identifies a TextChunk by its natural composite key (PdfDocumentId, ChunkIndex).
/// Matches the <c>ix_text_chunks_pdf_chunk_index</c> unique index on the EF entity.
/// </summary>
internal sealed record ChunkUsageLocator(Guid PdfDocumentId, int ChunkIndex);
