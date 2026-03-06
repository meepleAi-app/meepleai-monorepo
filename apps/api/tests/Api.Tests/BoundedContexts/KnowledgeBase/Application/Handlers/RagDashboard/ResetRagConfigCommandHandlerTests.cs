using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.RagDashboard;

// Issue #3305: RAG Dashboard Test Suite

/// <summary>
/// Unit tests for ResetRagConfigCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public class ResetRagConfigCommandHandlerTests
{
    private readonly Mock<IRagUserConfigRepository> _mockRepository;
    private readonly Mock<ILogger<ResetRagConfigCommandHandler>> _mockLogger;
    private readonly ResetRagConfigCommandHandler _handler;

    public ResetRagConfigCommandHandlerTests()
    {
        _mockRepository = new Mock<IRagUserConfigRepository>();
        _mockLogger = new Mock<ILogger<ResetRagConfigCommandHandler>>();
        _handler = new ResetRagConfigCommandHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ReturnsDefaultConfig()
    {
        // Arrange
        var command = new ResetRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Strategy = null
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Generation);
        Assert.NotNull(result.Retrieval);
        Assert.NotNull(result.Reranker);
        Assert.NotNull(result.Models);
        Assert.NotNull(result.StrategySpecific);
    }

    [Fact]
    public async Task Handle_WithNullStrategy_ReturnsHybridAsDefault()
    {
        // Arrange
        var command = new ResetRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Strategy = null
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Hybrid", result.ActiveStrategy);
    }

    [Theory]
    [InlineData("Semantic")]
    [InlineData("Keyword")]
    [InlineData("Contextual")]
    [InlineData("MultiQuery")]
    [InlineData("Agentic")]
    public async Task Handle_WithSpecificStrategy_ReturnsConfigForThatStrategy(string strategy)
    {
        // Arrange
        var command = new ResetRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Strategy = strategy
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(strategy, result.ActiveStrategy);
    }

    [Fact]
    public async Task Handle_ReturnsDefaultGenerationParams()
    {
        // Arrange
        var command = new ResetRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Strategy = null
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0.7, result.Generation.Temperature);
        Assert.Equal(40, result.Generation.TopK);
        Assert.Equal(0.9, result.Generation.TopP);
        Assert.Equal(1000, result.Generation.MaxTokens);
    }

    [Fact]
    public async Task Handle_ReturnsDefaultRetrievalParams()
    {
        // Arrange
        var command = new ResetRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Strategy = null
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(500, result.Retrieval.ChunkSize);
        Assert.Equal(10, result.Retrieval.ChunkOverlap);
        Assert.Equal(5, result.Retrieval.TopResults);
        Assert.Equal(0.7, result.Retrieval.SimilarityThreshold);
    }

    [Fact]
    public async Task Handle_ReturnsDefaultRerankerSettings()
    {
        // Arrange
        var command = new ResetRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Strategy = null
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Reranker.Enabled);
        Assert.Equal("cross-encoder/ms-marco-MiniLM-L-12-v2", result.Reranker.Model);
        Assert.Equal(10, result.Reranker.TopN);
    }

    [Fact]
    public async Task Handle_ReturnsDefaultModelSelection()
    {
        // Arrange
        var command = new ResetRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Strategy = null
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("gpt-4o-mini", result.Models.PrimaryModel);
        Assert.Null(result.Models.FallbackModel);
        Assert.Null(result.Models.EvaluationModel);
    }

    [Fact]
    public async Task Handle_ReturnsDefaultStrategySpecificSettings()
    {
        // Arrange
        var command = new ResetRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Strategy = null
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0.5, result.StrategySpecific.HybridAlpha);
        Assert.Equal(5, result.StrategySpecific.ContextWindow);
        Assert.Equal(3, result.StrategySpecific.MaxHops);
    }

    [Fact]
    public async Task Handle_LogsInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ResetRagConfigCommand
        {
            UserId = userId,
            Strategy = null
        };

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Resetting RAG config")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithStrategy_LogsStrategyName()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ResetRagConfigCommand
        {
            UserId = userId,
            Strategy = "Semantic"
        };

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Semantic")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
