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

    [Fact]
    public async Task Handle_EmptyLocators_NoOp_ReturnsZero()
    {
        await using var db = NewDb();
        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var result = await sut.Handle(
            new IncrementChunkUsageCountsCommand(Array.Empty<ChunkUsageLocator>()),
            CancellationToken.None);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NullLocators_NoOp_ReturnsZero()
    {
        await using var db = NewDb();
        var sut = new IncrementChunkUsageCountsCommandHandler(db, NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var result = await sut.Handle(
            new IncrementChunkUsageCountsCommand(null!),
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
            new IncrementChunkUsageCountsCommand(new[] { new ChunkUsageLocator(pdfId, 5) }),
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

        // Same locator passed thrice — DEC-D2 distinct-message scope must dedupe to +1.
        await sut.Handle(
            new IncrementChunkUsageCountsCommand(new[]
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
            new IncrementChunkUsageCountsCommand(new[] { new ChunkUsageLocator(Guid.NewGuid(), 99) }),
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
            new IncrementChunkUsageCountsCommand(new[]
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
            new IncrementChunkUsageCountsCommand(new[] { new ChunkUsageLocator(pdfA, 7) }),
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
            new IncrementChunkUsageCountsCommand(new[] { new ChunkUsageLocator(pdfId, 0) }),
            CancellationToken.None);

        var reloaded = await db.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id);
        reloaded.UsageCount.Should().Be(43);
    }
}
