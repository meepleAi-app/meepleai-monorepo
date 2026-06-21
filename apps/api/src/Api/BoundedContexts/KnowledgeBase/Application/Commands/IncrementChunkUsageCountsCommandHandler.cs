using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handles <see cref="IncrementChunkUsageCountsCommand"/> with distinct-thread
/// semantics (#2324 DEC-D2 upgrade of the BE-1 distinct-message scope).
///
/// <para><b>Algorithm</b> (per-locator, after intra-command dedup):
/// <list type="number">
///   <item><description>Resolve <see cref="ChunkUsageLocator"/> → chunk row(s).</description></item>
///   <item><description>If the (ThreadId, MessageId, ChunkId) junction row already
///     exists → no-op (replay protection).</description></item>
///   <item><description>If any OTHER message in the same thread already cited
///     this chunk (junction EXISTS check on <c>ix_chat_msg_chunk_thread_chunk</c>) →
///     record the junction row but DO NOT increment (distinct-thread rule).</description></item>
///   <item><description>Otherwise → first thread-touch: increment
///     <c>text_chunks.usage_count</c> and record the junction row.</description></item>
/// </list>
/// </para>
///
/// <para><b>Race condition</b> (documented, accepted): two parallel commands
/// targeting the same (ThreadId, ChunkId) from different messages may both
/// observe "no other message cited" and both increment, yielding a double
/// count. The counter is forward-looking telemetry (DEC-D2 start-from-0), not
/// a correctness invariant, and chat threads are essentially sequential — the
/// window is narrow. A serializable-isolation transaction or an advisory lock
/// would close the window but is out of scope for #2324 (P3).</para>
///
/// <para><b>Caller contract</b>: invoked OUTSIDE the chat thread's transaction
/// by design — a transient DB failure here MUST NOT roll back message
/// persistence. The caller catches and logs.</para>
/// </summary>
internal sealed class IncrementChunkUsageCountsCommandHandler
    : ICommandHandler<IncrementChunkUsageCountsCommand, int>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<IncrementChunkUsageCountsCommandHandler> _logger;

    public IncrementChunkUsageCountsCommandHandler(
        MeepleAiDbContext dbContext,
        ILogger<IncrementChunkUsageCountsCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<int> Handle(
        IncrementChunkUsageCountsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Locators is null || command.Locators.Count == 0)
        {
            return 0;
        }

        // Intra-message dedup: a chunk cited 3 times in the same assistant
        // response contributes a single junction row and (at most) a single
        // increment, regardless of distinct-thread state.
        var distinctLocators = command.Locators
            .Distinct()
            .ToList();

        var pdfDocumentIds = distinctLocators
            .Select(l => l.PdfDocumentId)
            .Distinct()
            .ToList();
        var chunkIndices = distinctLocators
            .Select(l => l.ChunkIndex)
            .Distinct()
            .ToList();

        // Two-axis prefilter — composite (PdfDocumentId, ChunkIndex) IN is not
        // expressible directly in EF, but the candidate window is small
        // (citation budget per chat turn ≈ 10-20 chunks).
        var locatorSet = distinctLocators.ToHashSet();

        var candidates = await _dbContext.TextChunks
            .Where(c => pdfDocumentIds.Contains(c.PdfDocumentId) && chunkIndices.Contains(c.ChunkIndex))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var matched = candidates
            .Where(c => locatorSet.Contains(new ChunkUsageLocator(c.PdfDocumentId, c.ChunkIndex)))
            .ToList();

        if (matched.Count == 0)
        {
            _logger.LogDebug(
                "IncrementChunkUsageCountsCommand: no matching chunks for {LocatorCount} locator(s) in thread {ThreadId}",
                distinctLocators.Count, command.ThreadId);
            return 0;
        }

        // Pre-load the junction slice for this (thread, chunks) cell so the
        // per-chunk decisions below stay in-memory. Mirrors the candidate
        // prefilter shape — bounded by the same citation budget.
        var matchedChunkIds = matched.Select(c => c.Id).ToList();
        var existingJunctionRows = await _dbContext.ChatMessageChunkCitations
            .Where(j => j.ThreadId == command.ThreadId && matchedChunkIds.Contains(j.ChunkId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int incremented = 0;
        foreach (var chunk in matched)
        {
            var chunkRows = existingJunctionRows.Where(j => j.ChunkId == chunk.Id).ToList();

            // Replay protection: (Thread, Message, Chunk) already recorded → no-op.
            var alreadyRecorded = chunkRows.Any(j => j.MessageId == command.MessageId);
            if (alreadyRecorded)
            {
                continue;
            }

            // Distinct-thread rule: any OTHER message in this thread already cited
            // the chunk → skip the increment but still record the junction row.
            var otherMessageCitedInThread = chunkRows.Any(j => j.MessageId != command.MessageId);

            _dbContext.ChatMessageChunkCitations.Add(new ChatMessageChunkCitationEntity
            {
                ThreadId = command.ThreadId,
                MessageId = command.MessageId,
                ChunkId = chunk.Id,
                CitedAt = DateTime.UtcNow,
            });

            if (!otherMessageCitedInThread)
            {
                chunk.UsageCount += 1;
                incremented++;
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Parallel-replay race: a sibling command holding a stale "snapshot empty"
            // view raced us to the junction insert. EF rolls back the whole change
            // set, so the counter is NOT double-ticked. Treat as a no-op — the
            // sibling's increment is the canonical one. Log at debug only.
            _logger.LogDebug(ex,
                "IncrementChunkUsageCountsCommand: junction insert raced (thread {ThreadId} message {MessageId}); rolled back without ticking",
                command.ThreadId, command.MessageId);
            return 0;
        }

        _logger.LogDebug(
            "IncrementChunkUsageCountsCommand: thread {ThreadId} message {MessageId} → {Incremented}/{Matched} chunks ticked (distinct-thread)",
            command.ThreadId, command.MessageId, incremented, matched.Count);

        return incremented;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
    {
        // Postgres SQLSTATE 23505 = unique_violation. Surfaces via Npgsql when
        // the composite PK on chat_message_chunk_citations rejects a duplicate
        // (ThreadId, MessageId, ChunkId) tuple.
        return ex.InnerException is PostgresException pg
            && string.Equals(pg.SqlState, "23505", StringComparison.Ordinal);
    }
}
