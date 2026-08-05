using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// #3435 (SP4): unit tests for the RAG table-chunk indexer. Uses an in-memory db + mocked embedding
/// service and vector store (the vector store is captured via .Callback to assert the wiring), so the
/// load-bearing branches — source_chunk_id wiring, ChunkIndex allocation, scoped replace, and the
/// no-game / no-vector-doc / embedding-failure paths — are covered without Postgres.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3435")]
public sealed class TableChunkIndexerTests
{
    private const string Markdown = "| a | b |\n|---|---|\n| 1 | 2 |";

    private static PdfDocumentEntity NewPdf(Guid? sharedGameId)
    {
        return new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "t.pdf",
            FilePath = "pdfs/abc/doc.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready",
            IndexerVersion = "v1",
            UploadedAt = DateTime.UtcNow,
            SharedGameId = sharedGameId,
        };
    }

    private static Mock<IEmbeddingService> EmbeddingSuccess()
    {
        var embed = new Mock<IEmbeddingService>();
        embed.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[768] }));
        embed.Setup(e => e.GetModelName()).Returns("test-model");
        return embed;
    }

    [Fact]
    public async Task IndexTableAsync_PersistsTableChunk_WithSourceChunkIdWiredAndMaxPlusOneIndex()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tci_{Guid.NewGuid():N}");
        var pdf = NewPdf(sharedGameId: Guid.NewGuid());
        db.PdfDocuments.Add(pdf);
        var vectorDocId = Guid.NewGuid();
        db.VectorDocuments.Add(new VectorDocumentEntity { Id = vectorDocId, PdfDocumentId = pdf.Id, SharedGameId = pdf.SharedGameId });
        // two narrative chunks (indexes 0,1) -> the table chunk must land at index 2.
        db.TextChunks.Add(new TextChunkEntity { Id = Guid.NewGuid(), PdfDocumentId = pdf.Id, Content = "n0", ChunkIndex = 0 });
        db.TextChunks.Add(new TextChunkEntity { Id = Guid.NewGuid(), PdfDocumentId = pdf.Id, Content = "n1", ChunkIndex = 1 });
        await db.SaveChangesAsync();

        List<Embedding>? captured = null;
        List<Guid>? deleted = null;
        var vectorStore = new Mock<IVectorStoreAdapter>();
        vectorStore.Setup(v => v.EnsureCollectionExistsAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        vectorStore.Setup(v => v.DeleteBySourceChunkIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Guid>, CancellationToken>((ids, _) => deleted = ids.ToList())
            .Returns(Task.CompletedTask);
        vectorStore.Setup(v => v.IndexBatchAsync(It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()))
            .Callback<List<Embedding>, CancellationToken>((list, _) => captured = list)
            .Returns(Task.CompletedTask);

        var indexer = new TableChunkIndexer(db, EmbeddingSuccess().Object, vectorStore.Object, NullLogger<TableChunkIndexer>.Instance);
        var regionHash = TableRegionKey.ComputeRegionHash(pdf.Id, 3, 0.1, 0.2, 0.8, 0.3);
        var expectedChunkId = TableRegionKey.ChunkIdFromRegionHash(regionHash);

        var result = await indexer.IndexTableAsync(pdf, 3, 0.1, 0.2, 0.8, 0.3, Markdown, regionHash, CancellationToken.None);

        result.Should().Be(expectedChunkId);

        var chunk = await db.TextChunks.AsNoTracking().FirstAsync(tc => tc.Id == expectedChunkId);
        chunk.ElementType.Should().Be("Table");
        chunk.Content.Should().Be(Markdown);
        chunk.PageNumber.Should().Be(3);
        chunk.ChunkIndex.Should().Be(2); // max(0,1) + 1
        chunk.BoundingBoxesJson.Should().NotBeNullOrEmpty();

        deleted.Should().Equal(expectedChunkId); // scoped replace, not delete-all-per-pdf
        captured.Should().ContainSingle();
        captured![0].SourceChunkId.Should().Be(expectedChunkId); // the JOIN key that carries the region bbox
        captured[0].VectorDocumentId.Should().Be(vectorDocId);
        captured[0].ChunkIndex.Should().Be(2);
    }

    [Fact]
    public async Task IndexTableAsync_ReturnsNull_WhenPdfHasNoGame_AndNeverEmbeds()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tci_{Guid.NewGuid():N}");
        var embed = EmbeddingSuccess();
        var indexer = new TableChunkIndexer(db, embed.Object, Mock.Of<IVectorStoreAdapter>(), NullLogger<TableChunkIndexer>.Instance);
        var pdf = NewPdf(sharedGameId: null); // no game -> not retrievable

        var result = await indexer.IndexTableAsync(pdf, 1, 0.1, 0.1, 0.5, 0.5, Markdown,
            TableRegionKey.ComputeRegionHash(pdf.Id, 1, 0.1, 0.1, 0.5, 0.5), CancellationToken.None);

        result.Should().BeNull();
        embed.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IndexTableAsync_ReturnsNull_WhenNoVectorDocument()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tci_{Guid.NewGuid():N}");
        var pdf = NewPdf(sharedGameId: Guid.NewGuid());
        db.PdfDocuments.Add(pdf); // has a game but NO vector document row
        await db.SaveChangesAsync();
        var embed = EmbeddingSuccess();

        var indexer = new TableChunkIndexer(db, embed.Object, Mock.Of<IVectorStoreAdapter>(), NullLogger<TableChunkIndexer>.Instance);
        var result = await indexer.IndexTableAsync(pdf, 1, 0.1, 0.1, 0.5, 0.5, Markdown,
            TableRegionKey.ComputeRegionHash(pdf.Id, 1, 0.1, 0.1, 0.5, 0.5), CancellationToken.None);

        result.Should().BeNull();
        embed.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IndexTableAsync_Throws_OnEmbeddingFailure_AndWritesNoChunk()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"tci_{Guid.NewGuid():N}");
        var pdf = NewPdf(sharedGameId: Guid.NewGuid());
        db.PdfDocuments.Add(pdf);
        db.VectorDocuments.Add(new VectorDocumentEntity { Id = Guid.NewGuid(), PdfDocumentId = pdf.Id, SharedGameId = pdf.SharedGameId });
        await db.SaveChangesAsync();

        var embed = new Mock<IEmbeddingService>();
        embed.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("embedding service down"));
        embed.Setup(e => e.GetModelName()).Returns("test-model");
        var vectorStore = new Mock<IVectorStoreAdapter>();

        var indexer = new TableChunkIndexer(db, embed.Object, vectorStore.Object, NullLogger<TableChunkIndexer>.Instance);
        var regionHash = TableRegionKey.ComputeRegionHash(pdf.Id, 1, 0.1, 0.1, 0.5, 0.5);
        var expectedChunkId = TableRegionKey.ChunkIdFromRegionHash(regionHash);

        var act = () => indexer.IndexTableAsync(pdf, 1, 0.1, 0.1, 0.5, 0.5, Markdown, regionHash, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await db.TextChunks.CountAsync(tc => tc.Id == expectedChunkId)).Should().Be(0); // no half-written chunk
        vectorStore.Verify(v => v.IndexBatchAsync(It.IsAny<List<Embedding>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
