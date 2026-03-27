using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.RagDashboard;

// Issue #3305: RAG Dashboard Test Suite

/// <summary>
/// Unit tests for SaveRagConfigCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagDashboard")]
public class SaveRagConfigCommandHandlerTests
{
    private readonly Mock<IRagUserConfigRepository> _mockRepository;
    private readonly Mock<ILogger<SaveRagConfigCommandHandler>> _mockLogger;
    private readonly SaveRagConfigCommandHandler _handler;

    public SaveRagConfigCommandHandlerTests()
    {
        _mockRepository = new Mock<IRagUserConfigRepository>();
        _mockRepository
            .Setup(r => r.UpsertAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RagUserConfigEntity());
        _mockLogger = new Mock<ILogger<SaveRagConfigCommandHandler>>();
        _handler = new SaveRagConfigCommandHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidConfig_ReturnsConfig()
    {
        // Arrange
        var config = CreateValidConfig();
        var command = new SaveRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Config = config
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ActiveStrategy.Should().Be(config.ActiveStrategy);
    }

    [Fact]
    public async Task Handle_ReturnsConfigAsIs()
    {
        // Arrange
        var config = CreateValidConfig();
        config = config with { ActiveStrategy = "Semantic" };

        var command = new SaveRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Config = config
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.ActiveStrategy.Should().Be("Semantic");
        result.Generation.Temperature.Should().Be(config.Generation.Temperature);
        result.Retrieval.ChunkSize.Should().Be(config.Retrieval.ChunkSize);
        result.Reranker.Model.Should().Be(config.Reranker.Model);
    }

    [Fact]
    public async Task Handle_LogsInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var config = CreateValidConfig();
        var command = new SaveRagConfigCommand
        {
            UserId = userId,
            Config = config
        };

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Saving RAG config")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Hybrid")]
    [InlineData("Semantic")]
    [InlineData("Keyword")]
    [InlineData("Contextual")]
    [InlineData("MultiQuery")]
    [InlineData("Agentic")]
    public async Task Handle_WithDifferentStrategies_SavesCorrectly(string strategy)
    {
        // Arrange
        var config = CreateValidConfig() with { ActiveStrategy = strategy };
        var command = new SaveRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Config = config
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.ActiveStrategy.Should().Be(strategy);
    }

    [Fact]
    public async Task Handle_PreservesGenerationParams()
    {
        // Arrange
        var config = CreateValidConfig();
        var command = new SaveRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Config = config
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Generation.Temperature.Should().Be(config.Generation.Temperature);
        result.Generation.TopK.Should().Be(config.Generation.TopK);
        result.Generation.TopP.Should().Be(config.Generation.TopP);
        result.Generation.MaxTokens.Should().Be(config.Generation.MaxTokens);
    }

    [Fact]
    public async Task Handle_PreservesRetrievalParams()
    {
        // Arrange
        var config = CreateValidConfig();
        var command = new SaveRagConfigCommand
        {
            UserId = Guid.NewGuid(),
            Config = config
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Retrieval.ChunkSize.Should().Be(config.Retrieval.ChunkSize);
        result.Retrieval.ChunkOverlap.Should().Be(config.Retrieval.ChunkOverlap);
        result.Retrieval.TopResults.Should().Be(config.Retrieval.TopResults);
        result.Retrieval.SimilarityThreshold.Should().Be(config.Retrieval.SimilarityThreshold);
    }

    private static RagConfigDto CreateValidConfig()
    {
        return new RagConfigDto
        {
            Generation = new GenerationParamsDto
            {
                Temperature = 0.7,
                TopK = 40,
                TopP = 0.9,
                MaxTokens = 1000
            },
            Retrieval = new RetrievalParamsDto
            {
                ChunkSize = 500,
                ChunkOverlap = 10,
                TopResults = 5,
                SimilarityThreshold = 0.7
            },
            Reranker = new RerankerSettingsDto
            {
                Enabled = true,
                Model = "cross-encoder/ms-marco-MiniLM-L-12-v2",
                TopN = 10
            },
            Models = new ModelSelectionDto
            {
                PrimaryModel = "gpt-4o-mini",
                FallbackModel = null,
                EvaluationModel = null
            },
            StrategySpecific = new StrategySpecificSettingsDto
            {
                HybridAlpha = 0.5,
                ContextWindow = 5,
                MaxHops = 3
            },
            ActiveStrategy = "Hybrid"
        };
    }
}
