namespace Api.Infrastructure.Entities;

/// <summary>
/// Junction row recording that an assistant <c>ChatMessage</c> (identified by
/// <see cref="MessageId"/>) cited a <c>TextChunk</c> inside a particular
/// <see cref="ThreadId"/>. The composite PK <c>(ThreadId, MessageId, ChunkId)</c>
/// guarantees idempotency on retries, and the secondary index
/// <c>(ThreadId, ChunkId)</c> backs the EXISTS check the handler uses to
/// decide whether the current message is the first one in the thread to cite
/// this chunk.
///
/// <para>Issue #2324 — DEC-D2 distinct-thread upgrade. The
/// <c>text_chunks.usage_count</c> column ticks once per distinct thread that
/// cites a chunk; the junction makes the EXISTS check efficient.</para>
///
/// <para><b>No <c>ChatMessage</c> FK</b>: messages are persisted as a JSON blob
/// inside <c>ChatThreadEntity.MessagesJson</c>, so MessageId here is a
/// referenceless value — application-level integrity only. ON DELETE CASCADE on
/// <see cref="ThreadId"/> cleans up the whole junction when a thread is dropped.</para>
/// </summary>
public class ChatMessageChunkCitationEntity
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid ChunkId { get; set; }
    // CitedAt is assigned by the handler at insert time; no property-level default
    // because EF would not project it as a column default (the handler is the
    // single source of truth for this value).
    public DateTime CitedAt { get; set; }

    // Navigation
    public ChatThreadEntity Thread { get; set; } = default!;
    public Api.Infrastructure.Entities.TextChunkEntity Chunk { get; set; } = default!;
}
