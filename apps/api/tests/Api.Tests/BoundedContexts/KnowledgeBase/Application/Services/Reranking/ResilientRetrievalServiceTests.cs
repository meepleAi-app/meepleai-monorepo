using Api.BoundedContexts.KnowledgeBase.Application.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.External.Reranking;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Reranking;

/// <summary>
/// Unit tests for ResilientRetrievalService.
/// ADR-016 Phase 4: Resilient retrieval with graceful degradation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ResilientRetrievalServiceTests
{
    private readonly Mock<IHybridSearchService> _hybridSearchMock;
    private readonly Mock<ICrossEncoderReranker> _rerankerMock;
    private readonly Mock<IParentChunkResolver> _parentResolverMock;
    private readonly Mock<HybridCache> _cacheMock;
    private readonly Mock<ILogger<ResilientRetrievalService>> _loggerMock;
    private readonly ResilientRetrievalOptions _options;

    public ResilientRetrievalServiceTests()
    {
        _hybridSearchMock = new Mock<IHybridSearchService>();
        _rerankerMock = new Mock<ICrossEncoderReranker>();
        _parentResolverMock = new Mock<IParentChunkResolver>();
        _cacheMock = new Mock<HybridCache>();
        _loggerMock = new Mock<ILogger<ResilientRetrievalService>>();
        _options = new ResilientRetrievalOptions
        {
            EnableReranking = true,
            CandidateMultiplier = 3,
            FailureThreshold = 3,
            HealthCheckIntervalSeconds = 30
        };
    }

    private ResilientRetrievalService CreateService()
    {
        return new ResilientRetrievalService(
            _hybridSearchMock.Object,
            _rerankerMock.Object,
            _parentResolverMock.Object,
            _loggerMock.Object,
            Options.Create(_options));
    }

    private static HybridSearchResult CreateSearchResult(string chunkId, string content, float score, Guid gameId)
    {
        return new HybridSearchResult
        {
            ChunkId = chunkId,
            Content = content,
            HybridScore = score,
            PdfDocumentId = Guid.NewGuid().ToString(),
            GameId = gameId,
            ChunkIndex = 0,
            Mode = SearchMode.Hybrid
        };
    }

    [Fact]
    public async Task RetrieveAsync_WithResults_ReturnsRerankedResults()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var service = CreateService();

        _hybridSearchMock
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                CreateSearchResult("chunk-1", "First result", 0.9f, gameId),
                CreateSearchResult("chunk-2", "Second result", 0.8f, gameId)
            });

        _rerankerMock
            .Setup(r => r.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _rerankerMock
            .Setup(r => r.RerankAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RerankChunk>>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RerankResult(
                Chunks: new List<RerankedChunk>
                {
                    new("chunk-1", "First result", 0.9, 0.95, null),
                    new("chunk-2", "Second result", 0.8, 0.85, null)
                },
                Model: "BAAI/bge-reranker-v2-m3",
                ProcessingTimeMs: 100));

        var request = new RerankedRetrievalRequest(
            Query: "test query",
            GameId: gameId,
            TopK: 5,
            Mode: RetrievalMode.Hybrid,
            ExpandToParent: false);

        // Act
        var result = await service.RetrieveAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().HaveCount(2);
        result.UsedReranker.Should().BeTrue();
    }

    [Fact]
    public async Task RetrieveAsync_WithEmptySearchResults_ReturnsEmptyResult()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var service = CreateService();

        _hybridSearchMock
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        var request = new RerankedRetrievalRequest(
            Query: "test query",
            GameId: gameId,
            TopK: 5,
            Mode: RetrievalMode.Hybrid,
            ExpandToParent: false);

        // Act
        var result = await service.RetrieveAsync(request);

        // Assert
        result.Results.Should().BeEmpty();
        result.UsedReranker.Should().BeFalse();
        result.FallbackReason.Should().Be("No search results");
    }

    [Fact]
    public async Task RetrieveAsync_WhenRerankerDisabled_UsesFallback()
    {
        // Arrange
        _options.EnableReranking = false;
        var gameId = Guid.NewGuid();
        var service = CreateService();

        _hybridSearchMock
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                CreateSearchResult("chunk-1", "First result", 0.9f, gameId)
            });

        var request = new RerankedRetrievalRequest(
            Query: "test query",
            GameId: gameId,
            TopK: 5,
            Mode: RetrievalMode.Hybrid,
            ExpandToParent: false);

        // Act
        var result = await service.RetrieveAsync(request);

        // Assert
        result.UsedReranker.Should().BeFalse();
        result.FallbackReason.Should().Be("Reranking disabled");
    }

    [Fact]
    public async Task RetrieveAsync_WhenRerankerUnavailable_UsesFallback()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var service = CreateService();

        _hybridSearchMock
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                CreateSearchResult("chunk-1", "First result", 0.9f, gameId)
            });

        _rerankerMock
            .Setup(r => r.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = new RerankedRetrievalRequest(
            Query: "test query",
            GameId: gameId,
            TopK: 5,
            Mode: RetrievalMode.Hybrid,
            ExpandToParent: false);

        // Act
        var result = await service.RetrieveAsync(request);

        // Assert
        result.UsedReranker.Should().BeFalse();
        result.FallbackReason.Should().Be("Reranker unavailable");
    }

    [Fact]
    public async Task RetrieveAsync_WithParentExpansion_ResolvesParents()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var service = CreateService();

        _hybridSearchMock
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                CreateSearchResult("chunk-1", "First result", 0.9f, gameId)
            });

        _rerankerMock
            .Setup(r => r.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _rerankerMock
            .Setup(r => r.RerankAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RerankChunk>>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RerankResult(
                Chunks: new List<RerankedChunk>
                {
                    new("chunk-1", "First result", 0.9, 0.95, null)
                },
                Model: "BAAI/bge-reranker-v2-m3",
                ProcessingTimeMs: 100));

        var metadata = new ChunkMetadata { DocumentId = documentId, Page = 1 };
        var parent = HierarchicalChunk.CreateParent("Parent content", metadata);
        var child = HierarchicalChunk.CreateChild("Child content", 2, metadata, parent.Id);

        _parentResolverMock
            .Setup(p => p.ResolveParentsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ResolvedParentChunk>
            {
                ResolvedParentChunk.FromChunks(parent, child)
            });

        var request = new RerankedRetrievalRequest(
            Query: "test query",
            GameId: gameId,
            TopK: 5,
            Mode: RetrievalMode.Hybrid,
            ExpandToParent: true);

        // Act
        var result = await service.RetrieveAsync(request);

        // Assert
        result.Results.Should().HaveCount(1);
        _parentResolverMock.Verify(
            p => p.ResolveParentsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_WhenRerankerThrows_FallsBackGracefully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var service = CreateService();

        _hybridSearchMock
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                CreateSearchResult("chunk-1", "First result", 0.9f, gameId)
            });

        _rerankerMock
            .Setup(r => r.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _rerankerMock
            .Setup(r => r.RerankAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RerankChunk>>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RerankerServiceException("Service unavailable"));

        var request = new RerankedRetrievalRequest(
            Query: "test query",
            GameId: gameId,
            TopK: 5,
            Mode: RetrievalMode.Hybrid,
            ExpandToParent: false);

        // Act
        var result = await service.RetrieveAsync(request);

        // Assert
        result.UsedReranker.Should().BeFalse();
        result.FallbackReason.Should().Contain("Service unavailable");
        result.Results.Should().NotBeEmpty(); // Should still have fallback results
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsCurrentStatus()
    {
        // Arrange
        var service = CreateService();

        _rerankerMock
            .Setup(r => r.IsHealthyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var status = await service.GetStatusAsync();

        // Assert
        status.Should().NotBeNull();
        status.RerankerAvailable.Should().BeTrue();
        status.RerankerModel.Should().Be("BAAI/bge-reranker-v2-m3");
    }

    [Fact]
    public async Task RetrieveAsync_WithDifferentModes_UsesCorrectSearchMode()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var service = CreateService();

        _hybridSearchMock
            .Setup(h => h.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                SearchMode.Semantic,
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        var request = new RerankedRetrievalRequest(
            Query: "test query",
            GameId: gameId,
            TopK: 5,
            Mode: RetrievalMode.Vector,
            ExpandToParent: false);

        // Act
        await service.RetrieveAsync(request);

        // Assert
        _hybridSearchMock.Verify(
            h => h.SearchAsync(
                "test query",
                gameId,
                SearchMode.Semantic,
                15, // topK * CandidateMultiplier (5 * 3)
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
