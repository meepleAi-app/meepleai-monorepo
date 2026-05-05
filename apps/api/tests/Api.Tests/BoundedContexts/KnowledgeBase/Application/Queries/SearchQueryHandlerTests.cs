using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for <see cref="SearchQueryHandler"/>.
///
/// Issue #563: Verifies that callers can supply a pre-computed query vector via
/// <see cref="SearchQuery.QueryVector"/> to avoid a duplicate embedding call,
/// and that the legacy fallback path (no caller vector → handler embeds) still works.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class SearchQueryHandlerTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    private static readonly Guid TestGameId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string TestQuery = "how do I win the game?";
    private const string TestLanguage = "en";

    /// <summary>
    /// Issue #563 — happy path: caller supplies a pre-computed query vector,
    /// so the handler MUST NOT invoke <see cref="IEmbeddingService.GenerateEmbeddingAsync(string, string, CancellationToken)"/>,
    /// and the supplied vector MUST flow through to <see cref="IEmbeddingRepository.SearchByVectorAsync"/>.
    /// </summary>
    [Fact]
    public async Task Handle_WithPrecomputedQueryVector_SkipsEmbeddingService()
    {
        // Arrange
        var precomputed = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };

        var embeddingServiceMock = new Mock<IEmbeddingService>(MockBehavior.Strict);
        // No setup: a strict mock will throw if GenerateEmbeddingAsync is invoked.

        var capturedVectors = new List<Vector>();
        var embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Vector, int, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, vector, _, _, _, _) => capturedVectors.Add(vector))
            .ReturnsAsync(new List<Embedding>());

        var ragAccessMock = new Mock<IRagAccessService>();
        // No UserId on query → access check is skipped, no setup needed.

        var handler = CreateHandler(
            embeddingRepositoryMock.Object,
            embeddingServiceMock.Object,
            ragAccessMock.Object);

        var query = new SearchQuery(
            GameId: TestGameId,
            Query: TestQuery,
            TopK: 5,
            MinScore: 0.55,
            SearchMode: "vector",
            Language: TestLanguage,
            QueryVector: precomputed);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();

        // The strict mock guarantees GenerateEmbeddingAsync was never called,
        // but verify explicitly for documentation / regression safety.
        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // The pre-computed vector must reach the repository unchanged.
        capturedVectors.Should().HaveCount(1);
        capturedVectors[0].Values.Should().Equal(precomputed);
    }

    /// <summary>
    /// Issue #563 — fallback path: when the caller does not supply a vector
    /// (legacy/default behavior), the handler MUST generate one via
    /// <see cref="IEmbeddingService"/> exactly once and forward it to the repository.
    /// </summary>
    [Fact]
    public async Task Handle_WithoutQueryVector_GeneratesEmbedding()
    {
        // Arrange
        var generated = new float[] { 0.9f, 0.8f, 0.7f, 0.6f };

        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { generated }));

        var capturedVectors = new List<Vector>();
        var embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, Vector, int, double, IReadOnlyList<Guid>?, CancellationToken>(
                (_, vector, _, _, _, _) => capturedVectors.Add(vector))
            .ReturnsAsync(new List<Embedding>());

        var ragAccessMock = new Mock<IRagAccessService>();

        var handler = CreateHandler(
            embeddingRepositoryMock.Object,
            embeddingServiceMock.Object,
            ragAccessMock.Object);

        var query = new SearchQuery(
            GameId: TestGameId,
            Query: TestQuery,
            TopK: 5,
            MinScore: 0.55,
            SearchMode: "vector",
            Language: TestLanguage,
            QueryVector: null);

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();

        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<CancellationToken>()),
            Times.Once);

        capturedVectors.Should().HaveCount(1);
        capturedVectors[0].Values.Should().Equal(generated);
    }

    /// <summary>
    /// Issue #563 — defensive: an empty (Count == 0) caller vector is treated as "no vector"
    /// (pattern is <c>{ Count: &gt; 0 }</c>), and the handler falls back to embedding generation.
    /// Guards against silently passing a zero-dim vector to vector search.
    /// </summary>
    [Fact]
    public async Task Handle_WithEmptyQueryVector_FallsBackToEmbedding()
    {
        // Arrange
        var generated = new float[] { 0.5f, 0.5f };

        var embeddingServiceMock = new Mock<IEmbeddingService>();
        embeddingServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { generated }));

        var embeddingRepositoryMock = new Mock<IEmbeddingRepository>();
        embeddingRepositoryMock
            .Setup(r => r.SearchByVectorAsync(
                It.IsAny<Guid>(),
                It.IsAny<Vector>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<IReadOnlyList<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Embedding>());

        var ragAccessMock = new Mock<IRagAccessService>();

        var handler = CreateHandler(
            embeddingRepositoryMock.Object,
            embeddingServiceMock.Object,
            ragAccessMock.Object);

        var query = new SearchQuery(
            GameId: TestGameId,
            Query: TestQuery,
            TopK: 5,
            MinScore: 0.55,
            SearchMode: "vector",
            Language: TestLanguage,
            QueryVector: Array.Empty<float>());

        // Act
        await handler.Handle(query, TestCancellationToken);

        // Assert: empty list ≠ supplied → embedding service is called.
        embeddingServiceMock.Verify(
            s => s.GenerateEmbeddingAsync(TestQuery, TestLanguage, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static SearchQueryHandler CreateHandler(
        IEmbeddingRepository embeddingRepository,
        IEmbeddingService embeddingService,
        IRagAccessService ragAccessService)
    {
        var vectorSearchService = new VectorSearchDomainService();
        var rrfFusionService = new RrfFusionDomainService();
        var hybridSearchService = new Mock<IHybridSearchService>().Object;
        var logger = new Mock<ILogger<SearchQueryHandler>>().Object;

        return new SearchQueryHandler(
            embeddingRepository,
            vectorSearchService,
            rrfFusionService,
            embeddingService,
            hybridSearchService,
            ragAccessService,
            logger);
    }
}
