using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure;

/// <summary>
/// Unit tests for EmbeddingRepository.SearchByMultipleGameIdsAsync.
/// Issue #1661: Expose cross-game vector search on domain repository interface.
/// Verifies delegation to IVectorStoreAdapter with correct parameter passing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class EmbeddingRepositoryTests
{
    private readonly Mock<IVectorStoreAdapter> _adapterMock;
    private readonly Mock<IDomainEventCollector> _eventCollectorMock;
    private readonly EmbeddingRepository _sut;

    public EmbeddingRepositoryTests()
    {
        _adapterMock = new Mock<IVectorStoreAdapter>();
        _eventCollectorMock = new Mock<IDomainEventCollector>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new MeepleAiDbContext(
            options,
            Mock.Of<MediatR.IMediator>(),
            _eventCollectorMock.Object,
            Mock.Of<IDataProtectionProvider>(),
            null);

        _sut = new EmbeddingRepository(dbContext, _eventCollectorMock.Object, _adapterMock.Object);
    }

    #region SearchByMultipleGameIdsAsync Tests

    [Fact]
    public async Task SearchByMultipleGameIdsAsync_WithValidGameIds_DelegatesToAdapter()
    {
        // Arrange
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var gameIds = new List<Guid> { gameId1, gameId2 };
        var queryVector = new Vector(new float[] { 0.1f, 0.2f, 0.3f });
        const int topK = 5;
        const double minScore = 0.7;

        var expected = new List<Embedding>
        {
            CreateEmbedding(),
            CreateEmbedding()
        };

        _adapterMock
            .Setup(a => a.SearchByMultipleGameIdsAsync(
                gameIds,
                queryVector,
                topK,
                minScore,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.SearchByMultipleGameIdsAsync(gameIds, queryVector, topK, minScore);

        // Assert
        result.Should().BeSameAs(expected);
        _adapterMock.Verify(
            a => a.SearchByMultipleGameIdsAsync(gameIds, queryVector, topK, minScore, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchByMultipleGameIdsAsync_WithDocumentIds_PassesDocumentIdsToAdapter()
    {
        // Arrange
        var gameIds = new List<Guid> { Guid.NewGuid() };
        var queryVector = new Vector(new float[] { 0.5f, 0.5f });
        const int topK = 10;
        const double minScore = 0.5;
        var documentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _adapterMock
            .Setup(a => a.SearchByMultipleGameIdsAsync(
                gameIds,
                queryVector,
                topK,
                minScore,
                documentIds,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding>());

        // Act
        await _sut.SearchByMultipleGameIdsAsync(gameIds, queryVector, topK, minScore, documentIds);

        // Assert — documentIds forwarded exactly
        _adapterMock.Verify(
            a => a.SearchByMultipleGameIdsAsync(gameIds, queryVector, topK, minScore, documentIds, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchByMultipleGameIdsAsync_PropagatesCancellationToken()
    {
        // Arrange
        var gameIds = new List<Guid> { Guid.NewGuid() };
        var queryVector = new Vector(new float[] { 0.1f });
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _adapterMock
            .Setup(a => a.SearchByMultipleGameIdsAsync(
                gameIds,
                queryVector,
                It.IsAny<int>(),
                It.IsAny<double>(),
                null,
                token))
            .ReturnsAsync(new List<Embedding>());

        // Act
        await _sut.SearchByMultipleGameIdsAsync(gameIds, queryVector, 5, 0.7, cancellationToken: token);

        // Assert — exact token forwarded
        _adapterMock.Verify(
            a => a.SearchByMultipleGameIdsAsync(
                gameIds,
                queryVector,
                It.IsAny<int>(),
                It.IsAny<double>(),
                null,
                token),
            Times.Once);
    }

    [Fact]
    public async Task SearchByMultipleGameIdsAsync_WithEmptyGameIds_ReturnsEmptyWithoutCallingAdapter()
    {
        // Arrange — early-return optimisation: empty gameIds → no adapter call
        var emptyGameIds = new List<Guid>();
        var queryVector = new Vector(new float[] { 0.1f, 0.2f });

        // Act
        var result = await _sut.SearchByMultipleGameIdsAsync(emptyGameIds, queryVector, 5, 0.7);

        // Assert
        result.Should().BeEmpty();
        _adapterMock.Verify(
            a => a.SearchByMultipleGameIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchByMultipleGameIdsAsync_ReturnsAdapterResultsUnmodified()
    {
        // Arrange
        var gameIds = new List<Guid> { Guid.NewGuid() };
        var queryVector = new Vector(new float[] { 0.9f, 0.1f });

        var adapterResults = new List<Embedding>
        {
            CreateEmbedding(),
            CreateEmbedding(),
            CreateEmbedding()
        };

        _adapterMock
            .Setup(a => a.SearchByMultipleGameIdsAsync(
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(adapterResults);

        // Act
        var result = await _sut.SearchByMultipleGameIdsAsync(gameIds, queryVector, 10, 0.6);

        // Assert — proxy pattern: no mapping, same reference
        result.Should().HaveCount(3);
        result.Should().BeSameAs(adapterResults);
    }

    #endregion

    #region Helpers

    private static Embedding CreateEmbedding() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "sample text content",
            new Vector(new float[] { 0.1f, 0.2f, 0.3f }),
            "nomic-embed-text",
            chunkIndex: 0,
            pageNumber: 1);

    #endregion
}
