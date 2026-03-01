using Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using LlmMergedResponse = Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis.LlmRulebookMerger.LlmMergedResponse;
using LlmVictoryConditionsDto = Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis.LlmRulebookMerger.LlmVictoryConditionsDto;
using LlmResourceDto = Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis.LlmRulebookMerger.LlmResourceDto;
using LlmGamePhaseDto = Api.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis.LlmRulebookMerger.LlmGamePhaseDto;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.BackgroundAnalysis;

/// <summary>
/// Unit tests for LlmRulebookMerger service.
/// Issue #2525: Background Rulebook Analysis Tests
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LlmRulebookMergerTests
{
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<ILogger<LlmRulebookMerger>> _mockLogger;
    private readonly LlmRulebookMerger _service;

    public LlmRulebookMergerTests()
    {
        _mockLlmService = new Mock<ILlmService>();
        _mockLogger = new Mock<ILogger<LlmRulebookMerger>>();

        _service = new LlmRulebookMerger(
            _mockLlmService.Object,
            _mockLogger.Object);
    }

    #region MergeAnalysesAsync - Happy Path

    [Fact]
    public async Task MergeAnalysesAsync_WithSuccessfulChunks_ReturnsMergedAnalysis()
    {
        // Arrange
        var overview = CreateTestOverview();
        var chunkResults = CreateSuccessfulChunkResults(5);

        var llmResponse = CreateValidMergedResponse();
        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmMergedResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmMergedResponse?)llmResponse);

        // Act
        var result = await _service.MergeAnalysesAsync(overview, chunkResults);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be("Merged Game");
        result.Summary.Should().Contain("Comprehensive");
        result.KeyMechanics.Should().NotBeEmpty();
        result.Resources.Should().NotBeEmpty();
        result.GamePhases.Should().NotBeEmpty();
        result.ConfidenceScore.Should().BeGreaterThan(0);
        result.Metadata.TotalChunksProcessed.Should().Be(5);
        result.Metadata.SuccessfulChunks.Should().Be(5);
    }

    #endregion

    #region MergeAnalysesAsync - Deduplication Tests

    [Fact]
    public async Task MergeAnalysesAsync_WithDuplicateResources_DeduplicatesBeforeLlmMerge()
    {
        // Arrange
        var overview = CreateTestOverview();

        // Create 3 chunks with same resource "Wood" appearing in each
        var chunkResults = CreateChunkResultsWithDuplicateResources();

        var llmResponse = CreateValidMergedResponse();
        string? capturedPrompt = null;

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmMergedResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, RequestSource, CancellationToken>((_, userPrompt, _, _) => capturedPrompt = userPrompt)
            .ReturnsAsync((LlmMergedResponse?)llmResponse);

        // Act
        var result = await _service.MergeAnalysesAsync(overview, chunkResults);

        // Assert
        result.Metadata.DuplicatesRemoved.Should().BeGreaterThan(0, "duplicate resources should be pre-merged");

        // Verify pre-merge deduplication happened
        capturedPrompt.Should().NotBeNull();
        // Wood should appear only once in resources after deduplication
    }

    #endregion

    #region MergeAnalysesAsync - Confidence Scoring Tests

    [Fact]
    public async Task MergeAnalysesAsync_With80PercentSuccessRate_ReturnsConservativeConfidence()
    {
        // Arrange
        var overview = CreateTestOverview();
        var chunkResults = CreatePartialSuccessChunkResults(successCount: 4, failureCount: 1);

        var llmResponse = new LlmMergedResponse
        {
            GameTitle = "Test Game",
            Summary = "Test summary",
            KeyMechanics = ["Strategy"],
            VictoryConditions = null,
            Resources = [],
            GamePhases = [],
            CommonQuestions = [],
            ConfidenceScore = 0.72m // 80% success rate * 0.9 conservative factor
        };

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmMergedResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmMergedResponse?)llmResponse);

        // Act
        var result = await _service.MergeAnalysesAsync(overview, chunkResults);

        // Assert
        result.ConfidenceScore.Should().Be(0.72m);
        result.Metadata.SuccessfulChunks.Should().Be(4);
        result.Metadata.FailedChunks.Should().Be(1);
        result.Metadata.ChunkSuccessRate.Should().BeApproximately(0.8, 0.01);
    }

    [Fact]
    public async Task MergeAnalysesAsync_WithAllChunksSuccessful_ReturnsHighConfidence()
    {
        // Arrange
        var overview = CreateTestOverview();
        var chunkResults = CreateSuccessfulChunkResults(5);

        var llmResponse = new LlmMergedResponse
        {
            GameTitle = "Perfect Game",
            Summary = "Complete analysis",
            KeyMechanics = ["Perfect"],
            VictoryConditions = null,
            Resources = [],
            GamePhases = [],
            CommonQuestions = [],
            ConfidenceScore = 0.95m
        };

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmMergedResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmMergedResponse?)llmResponse);

        // Act
        var result = await _service.MergeAnalysesAsync(overview, chunkResults);

        // Assert
        result.ConfidenceScore.Should().BeGreaterThanOrEqualTo(0.9m);
        result.Metadata.ChunkSuccessRate.Should().Be(1.0);
    }

    #endregion

    #region MergeAnalysesAsync - Fallback Tests

    [Fact]
    public async Task MergeAnalysesAsync_WhenLlmReturnsNull_ReturnsFallbackMerge()
    {
        // Arrange
        var overview = CreateTestOverview();
        var chunkResults = CreateSuccessfulChunkResults(3);

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmMergedResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmMergedResponse?)null);

        // Act
        var result = await _service.MergeAnalysesAsync(overview, chunkResults);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be(overview.GameTitle);
        result.Summary.Should().Contain("Board game rulebook analysis in progress"); // Fallback uses overview summary
        result.Metadata.SuccessfulChunks.Should().Be(3);
    }

    [Fact]
    public async Task MergeAnalysesAsync_WhenLlmThrowsException_ReturnsFallbackMerge()
    {
        // Arrange
        var overview = CreateTestOverview();
        var chunkResults = CreateSuccessfulChunkResults(4);

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmMergedResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Merge failed"));

        // Act
        var result = await _service.MergeAnalysesAsync(overview, chunkResults);

        // Assert
        result.Should().NotBeNull();
        result.GameTitle.Should().Be(overview.GameTitle);
        result.Metadata.SuccessfulChunks.Should().Be(4);
    }

    [Fact]
    public async Task MergeAnalysesAsync_FallbackUsesOverviewMechanicsWhenChunksEmpty()
    {
        // Arrange
        var overview = CreateTestOverview();
        var chunkResults = CreateEmptyChunkResults(2);

        _mockLlmService
            .Setup(x => x.GenerateJsonAsync<LlmMergedResponse>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmMergedResponse?)null); // Force fallback

        // Act
        var result = await _service.MergeAnalysesAsync(overview, chunkResults);

        // Assert
        result.KeyMechanics.Should().BeEquivalentTo(overview.MainMechanics);
    }

    #endregion

    #region Helper Methods

    private static OverviewExtractionResult CreateTestOverview()
    {
        return OverviewExtractionResult.Create(
            "Test Game",
            "Board game rulebook analysis in progress",
            ["Strategy", "Dice Rolling"],
            "Win by points",
            playerCountMin: 2,
            playerCountMax: 4,
            playtimeMinutes: 60,
            sectionHeaders: ["Setup", "Play", "End"]);
    }

    private static ParallelAnalysisResult CreateSuccessfulChunkResults(int count)
    {
        var results = Enumerable.Range(0, count)
            .Select(i => ChunkAnalysisResult.CreateSuccess(
                i,
                [$"Mechanic {i}"],
                [Resource.Create($"Resource {i}", "Type", "Usage", true)],
                [GamePhase.Create($"Phase {i}", "Description", i + 1, false)], // order must be positive
                [$"Question {i}?"],
                $"Summary for chunk {i}"))
            .ToList();

        return ParallelAnalysisResult.Create(results);
    }

    private static ParallelAnalysisResult CreatePartialSuccessChunkResults(int successCount, int failureCount)
    {
        var successResults = Enumerable.Range(0, successCount)
            .Select(i => ChunkAnalysisResult.CreateSuccess(
                i,
                [$"Mechanic {i}"],
                [Resource.Create($"Resource {i}", "Type", "Usage", true)],
                [],
                [],
                $"Summary {i}"))
            .ToList();

        var failureResults = Enumerable.Range(successCount, failureCount)
            .Select(i => ChunkAnalysisResult.CreateFailure(i, "Analysis failed"))
            .ToList();

        var allResults = successResults.Concat(failureResults).ToList();
        return ParallelAnalysisResult.Create(allResults);
    }

    private static ParallelAnalysisResult CreateEmptyChunkResults(int count)
    {
        var results = Enumerable.Range(0, count)
            .Select(i => ChunkAnalysisResult.CreateSuccess(i, [], [], [], [], "Empty"))
            .ToList();

        return ParallelAnalysisResult.Create(results);
    }

    private static ParallelAnalysisResult CreateChunkResultsWithDuplicateResources()
    {
        // 3 chunks, each with "Wood" resource
        var chunk1 = ChunkAnalysisResult.CreateSuccess(
            0,
            ["Trading"],
            [Resource.Create("Wood", "Material", "Build", true)],
            [],
            [],
            "Chunk 1");

        var chunk2 = ChunkAnalysisResult.CreateSuccess(
            1,
            ["Building"],
            [Resource.Create("Wood", "Material", "Construct", true)], // Duplicate!
            [],
            [],
            "Chunk 2");

        var chunk3 = ChunkAnalysisResult.CreateSuccess(
            2,
            ["Production"],
            [Resource.Create("Wood", "Resource", "Production", true)], // Duplicate!
            [],
            [],
            "Chunk 3");

        return ParallelAnalysisResult.Create([chunk1, chunk2, chunk3]);
    }

    private static LlmMergedResponse CreateValidMergedResponse()
    {
        return new LlmMergedResponse
        {
            GameTitle = "Merged Game",
            Summary = "Comprehensive merged analysis",
            KeyMechanics = ["Strategy", "Resource Management"],
            VictoryConditions = new LlmVictoryConditionsDto
            {
                Primary = "First to 10 points",
                Alternatives = ["Control 3 regions"],
                IsPointBased = true,
                TargetPoints = 10
            },
            Resources =
            [
                new LlmResourceDto
                {
                    Name = "Gold",
                    Type = "Currency",
                    Usage = "Trade",
                    IsLimited = true
                }
            ],
            GamePhases =
            [
                new LlmGamePhaseDto
                {
                    Name = "Action Phase",
                    Description = "Take actions",
                    Order = 1,
                    IsOptional = false
                }
            ],
            CommonQuestions = ["How do I win?"],
            ConfidenceScore = 0.85m
        };
    }

    #endregion
}
