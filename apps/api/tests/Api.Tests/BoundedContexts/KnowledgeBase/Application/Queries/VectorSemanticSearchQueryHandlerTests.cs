using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for VectorSemanticSearchQueryHandler.
/// Verifies embedding failure propagation, single-game search, and multi-game aggregation.
/// Task 4: Qdrant → pgvector migration.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class VectorSemanticSearchQueryHandlerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static VectorSemanticSearchQueryHandler CreateHandler(
        MeepleAiDbContext context,
        IEmbeddingService embeddingService,
        IVectorStoreAdapter vectorStoreAdapter)
    {
        var logger = new Mock<ILogger<VectorSemanticSearchQueryHandler>>().Object;
        return new VectorSemanticSearchQueryHandler(context, embeddingService, vectorStoreAdapter, logger);
    }

    // ──────────────────────────────────────────────
    // Test 1: Embedding service fails → error result
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_EmbeddingFails_ReturnsErrorResult()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext();

        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("embedding failed"));

        var vectorStoreMock = new Mock<IVectorStoreAdapter>();

        var handler = CreateHandler(context, embeddingServiceMock.Object, vectorStoreMock.Object);
        var query = new VectorSemanticSearchQuery("test query", 10, null);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().NotBeNull();
        result.ErrorMessage.Should().Contain("embedding failed");
        result.Results.Should().BeEmpty();

        vectorStoreMock.Verify(
            s => s.SearchAsync(It.IsAny<Guid>(), It.IsAny<Vector>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────
    // Test 2: With specific GameId → searches only that game
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithGameId_SearchesOnlyThatGame()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext();

        var gameId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        var floatVector = new float[768];
        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { floatVector }));

        var returnedEmbeddings = new List<Embedding>
        {
            new Embedding(
                id: Guid.NewGuid(),
                vectorDocumentId: docId,
                textContent: "some text content",
                vector: new Vector(floatVector),
                model: "e5-base",
                chunkIndex: 0,
                pageNumber: 1)
        };

        var vectorStoreMock = new Mock<IVectorStoreAdapter>();
        vectorStoreMock
            .Setup(s => s.SearchAsync(gameId, It.IsAny<Vector>(), 5, It.IsAny<double>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(returnedEmbeddings);

        var handler = CreateHandler(context, embeddingServiceMock.Object, vectorStoreMock.Object);
        var query = new VectorSemanticSearchQuery("test query", 5, gameId);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().BeNull();
        result.Results.Should().HaveCount(1);

        var item = result.Results[0];
        item.DocumentId.Should().Be(docId);
        item.Text.Should().Be("some text content");
        item.ChunkIndex.Should().Be(0);
        item.PageNumber.Should().Be(1);

        vectorStoreMock.Verify(
            s => s.SearchAsync(gameId, It.IsAny<Vector>(), 5, It.IsAny<double>(), null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ──────────────────────────────────────────────
    // Test 3: Without GameId → searches all games with completed vectors
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_WithoutGameId_SearchesAllCompletedGames()
    {
        // Arrange
        using var context = TestDbContextFactory.CreateInMemoryDbContext();

        var gameIdA = Guid.NewGuid();
        var gameIdB = Guid.NewGuid();
        var docIdA = Guid.NewGuid();
        var docIdB = Guid.NewGuid();

        // Seed VectorDocuments: 2 completed games, 1 failed (should be excluded)
        context.VectorDocuments.AddRange(
            new VectorDocumentEntity { Id = Guid.NewGuid(), GameId = gameIdA, IndexingStatus = "completed", PdfDocumentId = Guid.NewGuid(), ChunkCount = 5 },
            new VectorDocumentEntity { Id = Guid.NewGuid(), GameId = gameIdB, IndexingStatus = "completed", PdfDocumentId = Guid.NewGuid(), ChunkCount = 3 },
            new VectorDocumentEntity { Id = Guid.NewGuid(), GameId = Guid.NewGuid(), IndexingStatus = "failed", PdfDocumentId = Guid.NewGuid(), ChunkCount = 2 }
        );
        await context.SaveChangesAsync(TestCancellationToken);

        var floatVector = new float[768];
        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { floatVector }));

        var embeddingA = new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: docIdA,
            textContent: "text from game A",
            vector: new Vector(floatVector),
            model: "e5-base",
            chunkIndex: 0,
            pageNumber: 1);

        var embeddingB = new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: docIdB,
            textContent: "text from game B",
            vector: new Vector(floatVector),
            model: "e5-base",
            chunkIndex: 1,
            pageNumber: 2);

        var vectorStoreMock = new Mock<IVectorStoreAdapter>();
        vectorStoreMock
            .Setup(s => s.SearchByMultipleGameIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(gameIdA) && ids.Contains(gameIdB)),
                It.IsAny<Vector>(),
                10,
                It.IsAny<double>(),
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding> { embeddingA, embeddingB });

        var handler = CreateHandler(context, embeddingServiceMock.Object, vectorStoreMock.Object);
        var query = new VectorSemanticSearchQuery("test query", 10, null);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ErrorMessage.Should().BeNull();
        result.Results.Should().HaveCount(2);
        result.Results.Should().Contain(r => r.Text == "text from game A");
        result.Results.Should().Contain(r => r.Text == "text from game B");

        vectorStoreMock.Verify(
            s => s.SearchByMultipleGameIdsAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.Contains(gameIdA) && ids.Contains(gameIdB) && ids.Count == 2),
                It.IsAny<Vector>(),
                10,
                It.IsAny<double>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
