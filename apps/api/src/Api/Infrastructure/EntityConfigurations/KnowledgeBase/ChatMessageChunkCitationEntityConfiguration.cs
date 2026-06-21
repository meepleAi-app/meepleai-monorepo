using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations;

/// <summary>
/// EF Core configuration for <see cref="ChatMessageChunkCitationEntity"/>
/// (Issue #2324 — DEC-D2 distinct-thread upgrade of <c>text_chunks.usage_count</c>).
/// </summary>
internal class ChatMessageChunkCitationEntityConfiguration
    : IEntityTypeConfiguration<ChatMessageChunkCitationEntity>
{
    public void Configure(EntityTypeBuilder<ChatMessageChunkCitationEntity> builder)
    {
        builder.ToTable("chat_message_chunk_citations");

        // Composite PK — also the natural idempotency key. A retry of the same
        // (threadId, messageId, chunkId) tuple is a no-op (unique-violation on
        // SaveChanges, swallowed by the handler).
        builder.HasKey(e => new { e.ThreadId, e.MessageId, e.ChunkId });

        builder.Property(e => e.ThreadId).IsRequired();
        builder.Property(e => e.MessageId).IsRequired();
        builder.Property(e => e.ChunkId).IsRequired();
        builder.Property(e => e.CitedAt).IsRequired();

        // Thread FK with CASCADE delete: drop the thread → drop all its citations.
        builder.HasOne(e => e.Thread)
            .WithMany()
            .HasForeignKey(e => e.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chunk FK with CASCADE delete: drop the underlying chunk → drop its
        // junction rows (mirrors the issue body schema).
        builder.HasOne(e => e.Chunk)
            .WithMany()
            .HasForeignKey(e => e.ChunkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Secondary index for the "is this the first thread-touch?" EXISTS query.
        // Order matters: (ThreadId, ChunkId) lets Postgres index-only scan the
        // per-thread, per-chunk slice without bringing the message_id into play.
        builder.HasIndex(e => new { e.ThreadId, e.ChunkId })
            .HasDatabaseName("ix_chat_msg_chunk_thread_chunk");

        // Explicit FK-support index on ChunkId alone — backs the CASCADE scan
        // when a TextChunk is deleted. EF Core would auto-generate this for the
        // ChunkId FK, but declaring it explicitly preserves it across any future
        // migration squash / regeneration. The default name
        // (IX_chat_message_chunk_citations_ChunkId) matches the existing
        // migration so this is purely a configuration-side hardening — no
        // schema diff.
        builder.HasIndex(e => e.ChunkId);
    }
}
