using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LlmChunkResponse = Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis.LlmRulebookChunkAnalyzer.LlmChunkResponse;
using LlmResourceDto = Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis.LlmRulebookChunkAnalyzer.LlmResourceDto;
using LlmGamePhaseDto = Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis.LlmRulebookChunkAnalyzer.LlmGamePhaseDto;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Unit tests for LlmRulebookChunkAnalyzer service.
/// Issue #2525: Background Rulebook Analysis Tests
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LlmRulebookChunkAnalyzerTests
{
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<ILogger<LlmRulebookChunkAnalyzer>> _mockLogger;
    private readonly LlmRulebookChunkAnalyzer _service;

    public LlmRulebookChunkAnalyzerTests()
    {
        _mockLlmService = new Mock<ILlmService>();
        _mockLogger = new Mock<ILogger<LlmRulebookChunkAnalyzer>>();

        _service = new LlmRulebookChunkAnalyzer(
            _mockLlmService.Object,
            _mockLogger.Object);
    }

    #region AnalyzeChunkAsync - Single Chunk Tests

    [Fact]
    public async Task AnalyzeChunkAsync_WithValidChunk_ReturnsSuccessfulAnalysis()
    {
        // Arrange
        var chunk = CreateTestChunk(0, "Setup: Place board and distribute resources.");
        var gameContext = CreateTestGameContext();

        var llmResponse = CreateValidLlmChunkResponse();

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmChunkResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmChunkResponse?)llmResponse);

        // Act
        var result = await _service.AnalyzeChunkAsync(chunk, gameContext);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ChunkIndex.Should().Be(0);
        result.ExtractedMechanics.Should().Contain("Resource Distribution");
        result.Resources.Should().HaveCount(1);
        result.Resources[0].Name.Should().Be("Wood");
        result.GamePhases.Should().HaveCount(1);
        result.CommonQuestions.Should().HaveCount(1);
        result.ChunkSummary.Should().Be("Setup and resource distribution");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeChunkAsync_WhenLlmReturnsNull_ReturnsFailureResult()
    {
        // Arrange
        var chunk = CreateTestChunk(2, "Some content");
        var gameContext = CreateTestGameContext();

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmChunkResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmChunkResponse?)null);

        // Act
        var result = await _service.AnalyzeChunkAsync(chunk, gameContext);

        // Assert
        result.Success.Should().BeFalse();
        result.ChunkIndex.Should().Be(2);
        result.ErrorMessage.Should().Be("LLM returned null response");
    }

    [Fact]
    public async Task AnalyzeChunkAsync_WhenLlmThrowsException_ReturnsFailureResult()
    {
        // Arrange
        var chunk = CreateTestChunk(1, "Content");
        var gameContext = CreateTestGameContext();

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmChunkResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service error"));

        // Act
        var result = await _service.AnalyzeChunkAsync(chunk, gameContext);

        // Assert
        result.Success.Should().BeFalse();
        result.ChunkIndex.Should().Be(1);
        result.ErrorMessage.Should().Contain("LLM service error");
    }

    #endregion

    #region AnalyzeChunksParallelAsync - Parallel Processing Tests

    [Fact]
    public async Task AnalyzeChunksParallelAsync_With3Chunks_ProcessesAllSuccessfully()
    {
        // Arrange
        var chunks = new List<SemanticChunk>
        {
            CreateTestChunk(0, "Chunk 1 content"),
            CreateTestChunk(1, "Chunk 2 content"),
            CreateTestChunk(2, "Chunk 3 content")
        };
        var gameContext = CreateTestGameContext();

        var llmResponse = CreateValidLlmChunkResponse();
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmChunkResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _service.AnalyzeChunksParallelAsync(chunks, gameContext, maxParallelism: 2);

        // Assert
        result.Should().NotBeNull();
        result.Results.Should().HaveCount(3);
        result.SuccessCount.Should().Be(3);
        result.FailureCount.Should().Be(0);
        result.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public async Task AnalyzeChunksParallelAsync_WithProgressCallback_InvokesCallbackForEachChunk()
    {
        // Arrange
        var chunks = new List<SemanticChunk>
        {
            CreateTestChunk(0, "Chunk 1"),
            CreateTestChunk(1, "Chunk 2"),
            CreateTestChunk(2, "Chunk 3")
        };
        var gameContext = CreateTestGameContext();

        var llmResponse = CreateValidLlmChunkResponse();
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmChunkResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        var progressCallbacks = new List<(int processed, int total)>();
        Task ProgressCallback(int processed, int total)
        {
            progressCallbacks.Add((processed, total));
            return Task.CompletedTask;
        }

        // Act
        await _service.AnalyzeChunksParallelAsync(chunks, gameContext, progressCallback: ProgressCallback);

        // Assert
        progressCallbacks.Should().HaveCount(3);
        progressCallbacks.Should().Contain((1, 3));
        progressCallbacks.Should().Contain((2, 3));
        progressCallbacks.Should().Contain((3, 3));
    }

    [Fact]
    public async Task AnalyzeChunksParallelAsync_WithPartialFailures_ReturnsCorrectSuccessRate()
    {
        // Arrange
        var chunks = new List<SemanticChunk>
        {
            CreateTestChunk(0, "Chunk 1"),
            CreateTestChunk(1, "Chunk 2"),
            CreateTestChunk(2, "Chunk 3")
        };
        var gameContext = CreateTestGameContext();

        // Mock: First 2 succeed, last one fails
        var callCount = 0;
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmChunkResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount <= 2 ? CreateValidLlmChunkResponse() : null;
            });

        // Act
        var result = await _service.AnalyzeChunksParallelAsync(chunks, gameContext);

        // Assert
        result.Results.Should().HaveCount(3);
        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(1);
        result.SuccessRate.Should().BeApproximately(0.6667, 0.01); // 2/3 = 66.67%
    }

    [Fact]
    public async Task AnalyzeChunksParallelAsync_WithAllFailures_ReturnsZeroSuccessRate()
    {
        // Arrange
        var chunks = new List<SemanticChunk>
        {
            CreateTestChunk(0, "Chunk 1"),
            CreateTestChunk(1, "Chunk 2")
        };
        var gameContext = CreateTestGameContext();

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmChunkResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmChunkResponse?)null); // All fail

        // Act
        var result = await _service.AnalyzeChunksParallelAsync(chunks, gameContext);

        // Assert
        result.SuccessCount.Should().Be(0);
        result.FailureCount.Should().Be(2);
        result.SuccessRate.Should().Be(0.0);
    }

    [Fact]
    public async Task AnalyzeChunksParallelAsync_RespectsMaxParallelism()
    {
        // Arrange
        var chunks = new List<SemanticChunk>
        {
            CreateTestChunk(0, "Chunk 1"),
            CreateTestChunk(1, "Chunk 2"),
            CreateTestChunk(2, "Chunk 3"),
            CreateTestChunk(3, "Chunk 4"),
            CreateTestChunk(4, "Chunk 5")
        };
        var gameContext = CreateTestGameContext();

        var activeTasks = 0;
        var maxConcurrentTasks = 0;
        var lockObj = new object();

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmChunkResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, RequestSource _, CancellationToken _) =>
            {
                lock (lockObj)
                {
                    activeTasks++;
                    maxConcurrentTasks = Math.Max(maxConcurrentTasks, activeTasks);
                }

                await Task.Delay(50); // Simulate processing time

                lock (lockObj)
                {
                    activeTasks--;
                }

                return CreateValidLlmChunkResponse();
            });

        // Act
        await _service.AnalyzeChunksParallelAsync(chunks, gameContext, maxParallelism: 2);

        // Assert
        maxConcurrentTasks.Should().BeLessThanOrEqualTo(2, "should respect max parallelism limit");
    }

    #endregion

    #region Helper Methods

    private static SemanticChunk CreateTestChunk(int index, string content)
    {
        return SemanticChunk.Create(
            index,
            content,
            index * 1000,
            index * 1000 + content.Length,
            sectionHeader: $"Section {index}",
            contextHeaders: [$"Header {index}"]);
    }

    private static GameContext CreateTestGameContext()
    {
        return new GameContext(
            "Test Game",
            "A test board game",
            ["Strategy", "Dice"],
            "Win by points");
    }

    private static LlmChunkResponse CreateValidLlmChunkResponse()
    {
        return new LlmChunkResponse
        {
            ExtractedMechanics = ["Resource Distribution"],
            Resources =
            [
                new LlmResourceDto
                {
                    Name = "Wood",
                    Type = "Building",
                    Usage = "Build structures",
                    IsLimited = true
                }
            ],
            GamePhases =
            [
                new LlmGamePhaseDto
                {
                    Name = "Setup Phase",
                    Description = "Initial setup",
                    Order = 1,
                    IsOptional = false
                }
            ],
            CommonQuestions = ["How many resources do I start with?"],
            ChunkSummary = "Setup and resource distribution"
        };
    }

    #endregion
}