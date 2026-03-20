using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.RagDashboard;

// Issue #3305: RAG Dashboard Test Suite

/// <summary>
/// Unit tests for GetRagConfigQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public class GetRagConfigQueryHandlerTests
{
    private readonly Mock<IRagUserConfigRepository> _mockRepository;
    private readonly Mock<ILogger<GetRagConfigQueryHandler>> _mockLogger;
    private readonly GetRagConfigQueryHandler _handler;

    public GetRagConfigQueryHandlerTests()
    {
        _mockRepository = new Mock<IRagUserConfigRepository>();
        _mockLogger = new Mock<ILogger<GetRagConfigQueryHandler>>();
        _handler = new GetRagConfigQueryHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidUserId_ReturnsDefaultConfig()
    {
        // Arrange
        var query = new GetRagConfigQuery
        {
            UserId = Guid.NewGuid(),
            Strategy = null
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Generation);
        Assert.NotNull(result.Retrieval);
        Assert.NotNull(result.Reranker);
        Assert.NotNull(result.Models);
        Assert.NotNull(result.StrategySpecific);
    }

    [Fact]
    public async Task Handle_ReturnsConfigWithValidGenerationParams()
    {
        // Arrange
        var query = new GetRagConfigQuery { UserId = Guid.NewGuid() };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.InRange(result.Generation.Temperature, 0.0, 2.0);
        Assert.True(result.Generation.TopK > 0);
        Assert.InRange(result.Generation.TopP, 0.0, 1.0);
        Assert.True(result.Generation.MaxTokens > 0);
    }

    [Fact]
    public async Task Handle_ReturnsConfigWithValidRetrievalParams()
    {
        // Arrange
        var query = new GetRagConfigQuery { UserId = Guid.NewGuid() };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Retrieval.ChunkSize > 0);
        Assert.InRange(result.Retrieval.ChunkOverlap, 0, 50);
        Assert.True(result.Retrieval.TopResults > 0);
        Assert.InRange(result.Retrieval.SimilarityThreshold, 0.0, 1.0);
    }

    [Fact]
    public async Task Handle_ReturnsConfigWithValidRerankerSettings()
    {
        // Arrange
        var query = new GetRagConfigQuery { UserId = Guid.NewGuid() };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.Reranker.Model);
        Assert.True(result.Reranker.TopN > 0);
    }

    [Fact]
    public async Task Handle_ReturnsConfigWithValidModels()
    {
        // Arrange
        var query = new GetRagConfigQuery { UserId = Guid.NewGuid() };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result.Models.PrimaryModel);
    }

    [Fact]
    public async Task Handle_WithSpecificStrategy_ReturnsConfigForThatStrategy()
    {
        // Arrange
        var query = new GetRagConfigQuery
        {
            UserId = Guid.NewGuid(),
            Strategy = "Semantic"
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        result.ActiveStrategy.Should().Be("Semantic");
    }

    [Fact]
    public async Task Handle_WithNullStrategy_ReturnsHybridAsDefault()
    {
        // Arrange
        var query = new GetRagConfigQuery
        {
            UserId = Guid.NewGuid(),
            Strategy = null
        };

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.ActiveStrategy.Should().Be("Hybrid");
    }

    [Fact]
    public async Task Handle_LogsInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetRagConfigQuery { UserId = userId };

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Getting RAG config")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
