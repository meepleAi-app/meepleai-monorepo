using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Unit tests for <see cref="IncrementChunkUsageCountsCommandHandler"/>.
/// Issue #2311 BE-1 — chunk usage_count increment hook.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class IncrementChunkUsageCountsCommandHandlerTests
{
    private static MeepleAiDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"ChunkUsageInc_{Guid.NewGuid()}")
            .Options;
        return new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<IDomainEventCollector>());
    }

    private static TextChunkEntity NewChunk(Guid pdfId, int chunkIndex, int initialUsage = 0)
    {
        return new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            ChunkIndex = chunkIndex,
            Content = "snippet",
            CharacterCount = 7,
            CreatedAt = DateTime.UtcNow,
            UsageCount = initialUsage,
        };
    }

    // #2324 (DEC-D2 distinct-thread upgrade): every command now carries the
    // (ThreadId, MessageId) pair that the citation belongs to. Tests use fresh
    // Guids per scenario unless they need to share a thread to validate the
    // junction logic.
    private static IncrementChunkUsageCountsCommand NewCommand(
        IReadOnlyList<ChunkUsageLocator>? locators = null,
        Guid? threadId = null,
        Guid? messageId = null)
    {
        return new IncrementChunkUsageCountsCommand(
            threadId ?? Guid.NewGuid(),
            messageId ?? Guid.NewGuid(),
            locators!);
    }

    [Fact]
    public async Task Handle_EmptyLocators_NoOp_ReturnsZero()
    {
        await using var db = NewDb();
        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var result = await sut.Handle(
            NewCommand(Array.Empty<ChunkUsageLocator>()),
            CancellationToken.None);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NullLocators_NoOp_ReturnsZero()
    {
        await using var db = NewDb();
        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var result = await sut.Handle(
            NewCommand(locators: null),
            CancellationToken.None);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SingleMatchingLocator_IncrementsByOne()
    {
        await using var db = NewDb();
        var pdfId = Guid.NewGuid();
        var chunk = NewChunk(pdfId, chunkIndex: 5);
        db.TextChunks.Add(chunk);
        await db.SaveChangesAsync();

        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var result = await sut.Handle(
            NewCommand(new[] { new ChunkUsageLocator(pdfId, 5) }),
            CancellationToken.None);

        result.Should().Be(1);
        var reloaded = await db.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id);
        reloaded.UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DuplicateLocatorsInSameCommand_IncrementsAtMostOnce_DEC_D2()
    {
        await using var db = NewDb();
        var pdfId = Guid.NewGuid();
        var chunk = NewChunk(pdfId, chunkIndex: 3);
        db.TextChunks.Add(chunk);
        await db.SaveChangesAsync();

        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        // Same locator passed thrice — intra-message dedup must still hold under
        // the distinct-thread upgrade (one assistant message that cites the same
        // chunk three times contributes a single increment).
        await sut.Handle(
            NewCommand(new[]
            {
                new ChunkUsageLocator(pdfId, 3),
                new ChunkUsageLocator(pdfId, 3),
                new ChunkUsageLocator(pdfId, 3),
            }),
            CancellationToken.None);

        var reloaded = await db.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id);
        reloaded.UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownLocator_SilentlyMatchesZeroRows()
    {
        await using var db = NewDb();
        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var result = await sut.Handle(
            NewCommand(new[] { new ChunkUsageLocator(Guid.NewGuid(), 99) }),
            CancellationToken.None);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MixedLocators_OnlyMatchingRowsAreIncremented()
    {
        await using var db = NewDb();
        var pdfA = Guid.NewGuid();
        var pdfB = Guid.NewGuid();
        var chunkA = NewChunk(pdfA, chunkIndex: 1);
        var chunkB = NewChunk(pdfB, chunkIndex: 2);
        db.TextChunks.AddRange(chunkA, chunkB);
        await db.SaveChangesAsync();

        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        await sut.Handle(
            NewCommand(new[]
            {
                new ChunkUsageLocator(pdfA, 1),       // matches chunkA
                new ChunkUsageLocator(pdfA, 99),      // no row (same pdf, missing chunkIndex)
                new ChunkUsageLocator(pdfB, 2),       // matches chunkB
                new ChunkUsageLocator(Guid.NewGuid(), 2), // unknown pdf
            }),
            CancellationToken.None);

        var rows = await db.TextChunks.AsNoTracking().ToListAsync();
        rows.Single(r => r.Id == chunkA.Id).UsageCount.Should().Be(1);
        rows.Single(r => r.Id == chunkB.Id).UsageCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_CrossTalk_DoesNotIncrementWrongPdfWithMatchingChunkIndex()
    {
        // Regression guard: the bulk prefilter is (pdfDocumentIds, chunkIndices), so if
        // we only INNER-JOIN client-side correctly, an UNRELATED chunk on `pdfB` with the
        // same `ChunkIndex` as a locator targeting `pdfA` MUST NOT be touched.
        await using var db = NewDb();
        var pdfA = Guid.NewGuid();
        var pdfB = Guid.NewGuid();
        var chunkA = NewChunk(pdfA, chunkIndex: 7);
        var chunkB = NewChunk(pdfB, chunkIndex: 7); // same index, different pdf
        db.TextChunks.AddRange(chunkA, chunkB);
        await db.SaveChangesAsync();

        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        await sut.Handle(
            NewCommand(new[] { new ChunkUsageLocator(pdfA, 7) }),
            CancellationToken.None);

        var rows = await db.TextChunks.AsNoTracking().ToListAsync();
        rows.Single(r => r.Id == chunkA.Id).UsageCount.Should().Be(1);
        rows.Single(r => r.Id == chunkB.Id).UsageCount.Should().Be(0); // untouched
    }

    [Fact]
    public async Task Handle_PreservesExistingUsageCount_AddsOnTop()
    {
        await using var db = NewDb();
        var pdfId = Guid.NewGuid();
        var chunk = NewChunk(pdfId, chunkIndex: 0, initialUsage: 42);
        db.TextChunks.Add(chunk);
        await db.SaveChangesAsync();

        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        await sut.Handle(
            NewCommand(new[] { new ChunkUsageLocator(pdfId, 0) }),
            CancellationToken.None);

        var reloaded = await db.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id);
        reloaded.UsageCount.Should().Be(43);
    }

    // ─── #2324 DEC-D2 distinct-thread invariants ──────────────────────────────

    [Fact]
    [Trait("Issue", "2324")]
    public async Task Handle_ChunkCitedInTwoMessagesSameThread_IncrementsOnce()
    {
        // The crux of DEC-D2: the second message in a thread that cites the
        // same chunk MUST NOT inflate the counter — distinct-thread is the
        // contract. Junction row records both citations; counter ticks once.
        await using var db = NewDb();
        var pdfId = Guid.NewGuid();
        var chunk = NewChunk(pdfId, chunkIndex: 9);
        db.TextChunks.Add(chunk);
        await db.SaveChangesAsync();

        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var threadId = Guid.NewGuid();
        var locator = new ChunkUsageLocator(pdfId, 9);

        // First assistant message cites the chunk → +1.
        var first = await sut.Handle(
            NewCommand(new[] { locator }, threadId: threadId),
            CancellationToken.None);
        first.Should().Be(1);

        // Second message in the SAME thread cites the same chunk → no increment.
        var second = await sut.Handle(
            NewCommand(new[] { locator }, threadId: threadId),
            CancellationToken.None);
        second.Should().Be(0);

        var reloaded = await db.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id);
        reloaded.UsageCount.Should().Be(1);
    }

    [Fact]
    [Trait("Issue", "2324")]
    public async Task Handle_ChunkCitedInTwoDifferentThreads_IncrementsTwice()
    {
        // The same chunk being cited in two different threads represents two
        // distinct conversations → counter ticks twice.
        await using var db = NewDb();
        var pdfId = Guid.NewGuid();
        var chunk = NewChunk(pdfId, chunkIndex: 11);
        db.TextChunks.Add(chunk);
        await db.SaveChangesAsync();

        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);
        var locator = new ChunkUsageLocator(pdfId, 11);

        await sut.Handle(NewCommand(new[] { locator }, threadId: Guid.NewGuid()), CancellationToken.None);
        await sut.Handle(NewCommand(new[] { locator }, threadId: Guid.NewGuid()), CancellationToken.None);

        var reloaded = await db.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id);
        reloaded.UsageCount.Should().Be(2);
    }

    [Fact]
    [Trait("Issue", "2324")]
    public async Task Handle_SameMessageReplayed_IsIdempotent()
    {
        // Replay protection: a transient retry of the same (threadId, messageId)
        // must NOT double-count. The junction row's composite PK guarantees that
        // the second insert is a no-op.
        await using var db = NewDb();
        var pdfId = Guid.NewGuid();
        var chunk = NewChunk(pdfId, chunkIndex: 13);
        db.TextChunks.Add(chunk);
        await db.SaveChangesAsync();

        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var locator = new ChunkUsageLocator(pdfId, 13);

        await sut.Handle(
            NewCommand(new[] { locator }, threadId: threadId, messageId: messageId),
            CancellationToken.None);
        await sut.Handle(
            NewCommand(new[] { locator }, threadId: threadId, messageId: messageId),
            CancellationToken.None);

        var reloaded = await db.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id);
        reloaded.UsageCount.Should().Be(1);
    }
}
