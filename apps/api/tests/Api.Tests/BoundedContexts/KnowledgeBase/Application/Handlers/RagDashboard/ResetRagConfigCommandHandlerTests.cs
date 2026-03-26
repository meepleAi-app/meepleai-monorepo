using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
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
        result.Should().NotBeNull();
        result.Generation.Should().NotBeNull();
        result.Retrieval.Should().NotBeNull();
        result.Reranker.Should().NotBeNull();
        result.Models.Should().NotBeNull();
        result.StrategySpecific.Should().NotBeNull();
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
        result.ActiveStrategy.Should().Be("Hybrid");
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
        result.ActiveStrategy.Should().Be(strategy);
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
        result.Generation.Temperature.Should().Be(0.7);
        result.Generation.TopK.Should().Be(40);
        result.Generation.TopP.Should().Be(0.9);
        result.Generation.MaxTokens.Should().Be(1000);
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
        result.Retrieval.ChunkSize.Should().Be(500);
        result.Retrieval.ChunkOverlap.Should().Be(10);
        result.Retrieval.TopResults.Should().Be(5);
        result.Retrieval.SimilarityThreshold.Should().Be(0.7);
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
        result.Reranker.Enabled.Should().BeTrue();
        result.Reranker.Model.Should().Be("cross-encoder/ms-marco-MiniLM-L-12-v2");
        result.Reranker.TopN.Should().Be(10);
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
        result.Models.PrimaryModel.Should().Be("gpt-4o-mini");
        result.Models.FallbackModel.Should().BeNull();
        result.Models.EvaluationModel.Should().BeNull();
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
        result.StrategySpecific.HybridAlpha.Should().Be(0.5);
        result.StrategySpecific.ContextWindow.Should().Be(5);
        result.StrategySpecific.MaxHops.Should().Be(3);
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
