using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase;

/// <summary>
/// #3435 (SP4): sentence-window adjacency must ignore table chunks. Table chunks are appended PAST
/// the narrative reading-order range (ChunkIndex = maxNarrative + 1), so they are not
/// linear-adjacency neighbours — a table seed has none, and a table must never be pulled into a
/// narrative chunk's window (it would inject the last narrative chunk as if it were adjacent).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3435")]
public sealed class TextChunkSearchServiceAdjacencyTests
{
    private static TextChunkEntity Chunk(Guid pdf, int index, string element = "NarrativeText") => new()
    {
        Id = Guid.NewGuid(),
        PdfDocumentId = pdf,
        Content = $"c{index}",
        ChunkIndex = index,
        ElementType = element,
    };

    private static TextChunkSearchService Create(MeepleAiDbContext db)
        => new(db, NullLogger<TextChunkSearchService>.Instance);

    [Fact]
    public async Task GetAdjacentChunksAsync_ExcludesTableChunk_FromNarrativeWindow()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"adj_{Guid.NewGuid():N}");
        var pdf = Guid.NewGuid();
        db.TextChunks.AddRange(
            Chunk(pdf, 4),               // narrative seed
            Chunk(pdf, 3),               // real narrative neighbour
            Chunk(pdf, 5, "Table"));     // appended table at maxNarrative+1 — must be excluded
        await db.SaveChangesAsync();

        var adjacent = await Create(db).GetAdjacentChunksAsync(pdf, chunkIndex: 4, radius: 1, CancellationToken.None);

        adjacent.Select(a => a.ChunkIndex).Should().Equal(3); // table (5) excluded, seed (4) excluded
    }

    [Fact]
    public async Task GetAdjacentChunksAsync_ReturnsEmpty_WhenSeedIsTable()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"adj_{Guid.NewGuid():N}");
        var pdf = Guid.NewGuid();
        db.TextChunks.AddRange(
            Chunk(pdf, 4),               // last narrative
            Chunk(pdf, 5, "Table"));     // table seed
        await db.SaveChangesAsync();

        var adjacent = await Create(db).GetAdjacentChunksAsync(pdf, chunkIndex: 5, radius: 1, CancellationToken.None);

        adjacent.Should().BeEmpty(); // a table has no narrative reading-order neighbours
    }

    [Fact]
    public async Task GetAdjacentChunksAsync_NormalNarrativeAdjacency_StillWorks()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"adj_{Guid.NewGuid():N}");
        var pdf = Guid.NewGuid();
        db.TextChunks.AddRange(Chunk(pdf, 2), Chunk(pdf, 3), Chunk(pdf, 4));
        await db.SaveChangesAsync();

        var adjacent = await Create(db).GetAdjacentChunksAsync(pdf, chunkIndex: 3, radius: 1, CancellationToken.None);

        adjacent.Select(a => a.ChunkIndex).Should().BeEquivalentTo(new[] { 2, 4 });
    }
}
