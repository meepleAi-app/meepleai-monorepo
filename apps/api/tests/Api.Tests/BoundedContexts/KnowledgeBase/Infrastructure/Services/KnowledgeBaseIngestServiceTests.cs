using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Unit tests for <see cref="KnowledgeBaseIngestService"/>.
/// Libro Game AI Assistant MVP — Gap G3 ACL.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class KnowledgeBaseIngestServiceTests
{
    private readonly IVectorDocumentRepository _vdRepo = Substitute.For<IVectorDocumentRepository>();
    private readonly IEmbeddingRepository _embRepo = Substitute.For<IEmbeddingRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private KnowledgeBaseIngestService CreateSut() =>
        new(_vdRepo, _embRepo, _uow, NullLogger<KnowledgeBaseIngestService>.Instance);

    private static ChunkIngestionRequest MakeRequest(
        int chunkIndex = 0,
        int pageNumber = 1,
        string text = "Some chunk text.",
        string language = "en",
        string model = "e5-base-v2") =>
        new(
            PageNumber: pageNumber,
            ChunkIndex: chunkIndex,
            TextContent: text,
            Embedding: new float[768],
            Language: language,
            EmbeddingModel: model,
            OcrConfidence: 0.95f);

    // ─────────────────────────────────────────────────────────────────────────
    // IngestChunksAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IngestChunksAsync_NoExistingVectorDocument_CreatesNewAndPersistsEmbeddings()
    {
        // ARRANGE
        var sourceId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var requests = new[]
        {
            MakeRequest(chunkIndex: 0, text: "First chunk"),
            MakeRequest(chunkIndex: 1, text: "Second chunk"),
            MakeRequest(chunkIndex: 2, text: "Third chunk"),
        };

        // No existing VectorDocument for this (game, source) pair.
        _vdRepo.GetByGameAndSourceAsync(gameId, sourceId, Arg.Any<CancellationToken>())
            .Returns((VectorDocument?)null);

        var sut = CreateSut();

        // ACT
        var count = await sut.IngestChunksAsync(sourceId, gameId, requests, CancellationToken.None);

        // ASSERT
        count.Should().Be(3);

        // VectorDocument created once.
        await _vdRepo.Received(1).AddAsync(
            Arg.Is<VectorDocument>(vd => vd.GameId == gameId && vd.PdfDocumentId == sourceId),
            Arg.Any<CancellationToken>());

        // Embeddings bulk-inserted once with 3 entries.
        await _embRepo.Received(1).AddBatchAsync(
            Arg.Is<List<Embedding>>(list => list.Count == 3),
            Arg.Any<CancellationToken>());

        // SaveChanges called exactly once.
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IngestChunksAsync_ExistingVectorDocument_ReusesDocumentAndAppendsEmbeddings()
    {
        // ARRANGE
        var sourceId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var existingVd = new VectorDocument(
            id: Guid.NewGuid(),
            gameId: gameId,
            pdfDocumentId: sourceId,
            language: "en",
            totalChunks: 5);

        _vdRepo.GetByGameAndSourceAsync(gameId, sourceId, Arg.Any<CancellationToken>())
            .Returns(existingVd);

        var requests = new[]
        {
            MakeRequest(chunkIndex: 5, text: "New chunk A"),
            MakeRequest(chunkIndex: 6, text: "New chunk B"),
        };

        var sut = CreateSut();

        // ACT
        var count = await sut.IngestChunksAsync(sourceId, gameId, requests, CancellationToken.None);

        // ASSERT
        count.Should().Be(2);

        // No new VectorDocument should be created (existing reused).
        await _vdRepo.DidNotReceive().AddAsync(Arg.Any<VectorDocument>(), Arg.Any<CancellationToken>());

        // Embeddings bulk-inserted for the 2 new requests.
        await _embRepo.Received(1).AddBatchAsync(
            Arg.Is<List<Embedding>>(list => list.Count == 2),
            Arg.Any<CancellationToken>());

        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IngestChunksAsync_EmptyRequests_ReturnsZeroWithoutRepositoryCalls()
    {
        // ARRANGE
        var sut = CreateSut();

        // ACT
        var count = await sut.IngestChunksAsync(
            Guid.NewGuid(), Guid.NewGuid(),
            Array.Empty<ChunkIngestionRequest>(),
            CancellationToken.None);

        // ASSERT
        count.Should().Be(0);

        await _vdRepo.DidNotReceive().GetByGameAndSourceAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _embRepo.DidNotReceive().AddBatchAsync(
            Arg.Any<List<Embedding>>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RemoveBySourceAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveBySourceAsync_VectorDocumentExists_DeletesEmbeddingsAndDocument()
    {
        // ARRANGE
        var sourceId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var vdId = Guid.NewGuid();
        var existingVd = new VectorDocument(
            id: vdId,
            gameId: gameId,
            pdfDocumentId: sourceId,
            language: "en",
            totalChunks: 5);

        _vdRepo.GetByGameAndSourceAsync(gameId, sourceId, Arg.Any<CancellationToken>())
            .Returns(existingVd);

        var sut = CreateSut();

        // ACT
        var count = await sut.RemoveBySourceAsync(sourceId, gameId, CancellationToken.None);

        // ASSERT
        count.Should().Be(5);

        await _embRepo.Received(1).DeleteByVectorDocumentIdAsync(vdId, Arg.Any<CancellationToken>());
        await _vdRepo.Received(1).DeleteAsync(vdId, Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveBySourceAsync_VectorDocumentNotFound_ReturnsZeroGracefully()
    {
        // ARRANGE
        var sourceId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _vdRepo.GetByGameAndSourceAsync(gameId, sourceId, Arg.Any<CancellationToken>())
            .Returns((VectorDocument?)null);

        var sut = CreateSut();

        // ACT
        var count = await sut.RemoveBySourceAsync(sourceId, gameId, CancellationToken.None);

        // ASSERT
        count.Should().Be(0);

        await _embRepo.DidNotReceive().DeleteByVectorDocumentIdAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _vdRepo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
