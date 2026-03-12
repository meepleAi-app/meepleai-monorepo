using Api.BoundedContexts.KnowledgeBase.Application.Queries.EstimateAgentCost;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.EstimateAgentCost;

/// <summary>
/// Tests for EstimateAgentCostQueryHandler.
/// Validates pre-chat cost estimation logic for RAG agent sessions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class EstimateAgentCostQueryHandlerTests
{
    private readonly Mock<IVectorDocumentRepository> _mockVectorDocumentRepository;
    private readonly Mock<ILogger<EstimateAgentCostQueryHandler>> _mockLogger;
    private readonly EstimateAgentCostQueryHandler _handler;

    public EstimateAgentCostQueryHandlerTests()
    {
        _mockVectorDocumentRepository = new Mock<IVectorDocumentRepository>();
        _mockLogger = new Mock<ILogger<EstimateAgentCostQueryHandler>>();
        _handler = new EstimateAgentCostQueryHandler(
            _mockVectorDocumentRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_NoDocuments_ReturnsZeroCost()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid() };
        var query = new EstimateAgentCostQuery(gameId, documentIds);

        _mockVectorDocumentRepository
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalChunks);
        Assert.Equal(0, result.EstimatedEmbeddingTokens);
        Assert.Equal(0m, result.EstimatedCostPerQuery);
        Assert.Equal("USD", result.Currency);
        Assert.Contains("No indexed chunks found", result.Note);

        _mockVectorDocumentRepository.Verify(
            r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithDocuments_ReturnsCalculatedCost()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfDocId1 = Guid.NewGuid();
        var pdfDocId2 = Guid.NewGuid();
        var documentIds = new List<Guid> { pdfDocId1, pdfDocId2 };

        var vectorDoc1 = new VectorDocument(
            id: Guid.NewGuid(),
            gameId: gameId,
            pdfDocumentId: pdfDocId1,
            language: "en",
            totalChunks: 20);

        var vectorDoc2 = new VectorDocument(
            id: Guid.NewGuid(),
            gameId: gameId,
            pdfDocumentId: pdfDocId2,
            language: "en",
            totalChunks: 30);

        var query = new EstimateAgentCostQuery(gameId, documentIds, "VectorSearch");

        _mockVectorDocumentRepository
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { vectorDoc1, vectorDoc2 });

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(50, result.TotalChunks); // 20 + 30
        Assert.True(result.EstimatedEmbeddingTokens > 0);
        Assert.True(result.EstimatedCostPerQuery > 0m);
        Assert.Equal("USD", result.Currency);
        Assert.Contains("nomic-embed-text", result.Model);
        Assert.Contains("gpt-4o-mini", result.Model);
        Assert.Contains("top-5", result.Note);
        Assert.Contains("50 chunks", result.Note);
    }

    [Fact]
    public async Task Handle_HybridSearchStrategy_IncludesSearchCost()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfDocId = Guid.NewGuid();
        var documentIds = new List<Guid> { pdfDocId };

        var vectorDoc = new VectorDocument(
            id: Guid.NewGuid(),
            gameId: gameId,
            pdfDocumentId: pdfDocId,
            language: "en",
            totalChunks: 10);

        _mockVectorDocumentRepository
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { vectorDoc });

        var hybridQuery = new EstimateAgentCostQuery(gameId, documentIds, "HybridSearch");
        var vectorQuery = new EstimateAgentCostQuery(gameId, documentIds, "VectorSearch");

        // Act
        var hybridResult = await _handler.Handle(hybridQuery, TestContext.Current.CancellationToken);
        var vectorResult = await _handler.Handle(vectorQuery, TestContext.Current.CancellationToken);

        // Assert - HybridSearch should cost more due to reranking
        Assert.True(hybridResult.EstimatedCostPerQuery > vectorResult.EstimatedCostPerQuery,
            "HybridSearch should have higher cost than VectorSearch due to reranking");
        Assert.Contains("HybridSearch", hybridResult.Note);
        Assert.Contains("reranking", hybridResult.Note);
    }
}
