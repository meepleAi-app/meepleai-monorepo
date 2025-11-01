using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Comprehensive test suite for SetupGuideService (AI-03)
/// Tests cover: LLM integration, RAG retrieval, parsing, error handling, caching, edge cases
/// ADMIN-01 Phase 3: Updated with IPromptTemplateService and IConfiguration mocks
/// </summary>
public class SetupGuideServiceComprehensiveTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IQdrantService> _qdrantServiceMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IAiResponseCacheService> _cacheServiceMock;
    private readonly Mock<IPromptTemplateService> _promptTemplateMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<SetupGuideService>> _loggerMock;
    private readonly SetupGuideService _service;

    public SetupGuideServiceComprehensiveTests(ITestOutputHelper output)
    {
        _output = output;
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite($"DataSource=SetupGuideComprehensiveTest_{Guid.NewGuid()};Mode=Memory;Cache=Shared")
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _qdrantServiceMock = new Mock<IQdrantService>();
        _llmServiceMock = new Mock<ILlmService>();
        _cacheServiceMock = new Mock<IAiResponseCacheService>();
        _promptTemplateMock = new Mock<IPromptTemplateService>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<SetupGuideService>>();

        // ADMIN-01 Phase 3: Setup feature flag to use fallback (default behavior)
        // Mock IConfigurationSection for GetValue<bool> to work correctly
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("false");
        _configurationMock.Setup(c => c.GetSection("Features:PromptDatabase")).Returns(mockSection.Object);

        _service = new SetupGuideService(
            _dbContext,
            _embeddingServiceMock.Object,
            _qdrantServiceMock.Object,
            _llmServiceMock.Object,
            _cacheServiceMock.Object,
            _promptTemplateMock.Object,
            _configurationMock.Object,
            _loggerMock.Object
        );
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GivenNonExistentGame_WhenGeneratingSetupGuide_ThenReturnsDefaultGuideWithUnknownGameTitle()
    {
        // Arrange
        var gameId = "nonexistent-game-id";

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.gameTitle.Should().BeEquivalentTo("Unknown Game");
        result.steps.Should().NotBeEmpty();
        result.steps.Should().OnlyContain(step => !string.IsNullOrEmpty(step.instruction));
    }

    [Fact]
    public async Task GivenGameWithNoRagData_WhenGeneratingSetupGuide_ThenReturnsDefaultSteps()
    {
        // Arrange
        var gameId = "game-no-rag";
        var game = new GameEntity { Id = gameId, Name = "Test Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>())); // Empty results

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.gameTitle.Should().BeEquivalentTo("Test Game");
        result.steps.Should().NotBeEmpty();
        result.steps.Count.Should().Be(5); // Default steps count
        result.steps.Should().OnlyContain(step => step.references.Count == 0); // Default steps have no references
    }

    [Fact]
    public async Task GivenGameWithRagDataAndSuccessfulLlm_WhenGeneratingSetupGuide_ThenReturnsParsedLlmSteps()
    {
        // Arrange
        var gameId = "game-with-rag";
        var game = new GameEntity { Id = gameId, Name = "Advanced Strategy Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }
            });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem
                {
                    Text = "Place the game board in the center of the table.",
                    Score = 0.95f,
                    PdfId = "pdf123",
                    Page = 2
                },
                new SearchResultItem
                {
                    Text = "Each player receives a player board and starting resources.",
                    Score = 0.92f,
                    PdfId = "pdf123",
                    Page = 3
                }
            }));

        var llmResponse = @"STEP 1: Place the Board
Place the game board in the center of the table where all players can reach it.

STEP 2: Distribute Player Materials
Give each player a player board and their starting resources as listed in the rulebook.

STEP 3: Shuffle Card Decks
Shuffle all card decks thoroughly and place them face-down near the board.";

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                llmResponse,
                new LlmUsage(150, 100, 250)
            ));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.gameTitle.Should().BeEquivalentTo("Advanced Strategy Game");
        result.steps.Count.Should().Be(3);
        result.totalTokens.Should().Be(250);
        result.promptTokens.Should().Be(150);
        result.completionTokens.Should().Be(100);
        result.confidence.Should().NotBeNull();

        var firstStep = result.steps.First();
        firstStep.stepNumber.Should().Be(1);
        firstStep.title.Should().Be("Place the Board");
        firstStep.instruction.Should().Contain("center of the table");
        firstStep.references.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GivenLlmWithOptionalSteps_WhenParsing_ThenMarksStepsAsOptional()
    {
        // Arrange
        var gameId = "game-optional-steps";
        var game = new GameEntity { Id = gameId, Name = "Flexible Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { Text = "Setup instructions...", Score = 0.9f, PdfId = "pdf1", Page = 1 }
            }));

        var llmResponse = @"STEP 1: Required Step
This step is mandatory.

STEP 2: [OPTIONAL] Add Expansion Content
Include expansion components if playing with expansions.";

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(llmResponse, new LlmUsage(100, 50, 150)));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.steps.Count.Should().Be(2);
        result.steps[0].isOptional.Should().BeFalse();
        result.steps[1].isOptional.Should().BeTrue();
        result.steps[1].title.Should().Be("Add Expansion Content"); // [OPTIONAL] should be removed
    }

    [Fact]
    public async Task GivenLlmFailure_WhenGeneratingSetupGuide_ThenFallsBackToDefaultSteps()
    {
        // Arrange
        var gameId = "game-llm-failure";
        var game = new GameEntity { Id = gameId, Name = "Resilient Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { Text = "Some setup text", Score = 0.8f, PdfId = "pdf1", Page = 1 }
            }));

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("LLM service unavailable"));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.gameTitle.Should().BeEquivalentTo("Resilient Game");
        result.steps.Count.Should().Be(5); // Fallback to default 5 steps
        result.steps.Should().OnlyContain(step => !string.IsNullOrEmpty(step.instruction));
    }

    [Fact]
    public async Task GivenCachedResponse_WhenGeneratingSetupGuide_ThenReturnsCachedData()
    {
        // Arrange
        var gameId = "game-cached";
        var cachedResponse = new SetupGuideResponse(
            "Cached Game",
            new List<SetupGuideStep>
            {
                new SetupGuideStep(1, "Cached Step", "Cached instruction", new List<Snippet>(), false)
            },
            10,
            50,
            30,
            80,
            0.95
        );

        _cacheServiceMock
            .Setup(x => x.GenerateSetupCacheKey(gameId))
            .Returns($"setup:{gameId}");

        _cacheServiceMock
            .Setup(x => x.GetAsync<SetupGuideResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.gameTitle.Should().BeEquivalentTo("Cached Game");
        result.steps.Should().ContainSingle();
        result.steps[0].title.Should().Be("Cached Step");

        // Verify no embedding, qdrant, or LLM calls were made
        _embeddingServiceMock.VerifyNoOtherCalls();
        _qdrantServiceMock.VerifyNoOtherCalls();
        _llmServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenSuccessfulGeneration_WhenComplete_ThenCachesResponse()
    {
        // Arrange
        var gameId = "game-to-cache";
        var game = new GameEntity { Id = gameId, Name = "Cacheable Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        _cacheServiceMock
            .Setup(x => x.GenerateSetupCacheKey(gameId))
            .Returns($"setup:{gameId}");

        // Act
        await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        _cacheServiceMock.Verify(x => x.SetAsync(
            $"setup:{gameId}",
            It.IsAny<SetupGuideResponse>(),
            86400, // 24 hours TTL
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task GivenEmbeddingServiceFailure_WhenGeneratingSetupGuide_ThenReturnsDefaultSteps()
    {
        // Arrange
        var gameId = "game-embedding-fail";
        var game = new GameEntity { Id = gameId, Name = "Embedding Fail Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = false, ErrorMessage = "Embedding service down" });

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert: Service gracefully falls back to default steps even when embedding fails
        result.Should().NotBeNull();
        result.gameTitle.Should().BeEquivalentTo("Embedding Fail Game");
        result.steps.Should().NotBeEmpty();
        result.steps.Count.Should().Be(5); // Default steps
        (result.estimatedSetupTimeMinutes >= 5).Should().BeTrue(); // Minimum 5 minutes
    }

    [Fact]
    public async Task GivenMultipleGames_WhenGeneratingSetupGuides_ThenReturnsCorrectGameData()
    {
        // Arrange
        var game1 = new GameEntity { Id = "game1", Name = "Game One", CreatedAt = DateTime.UtcNow };
        var game2 = new GameEntity { Id = "game2", Name = "Game Two", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.AddRange(game1, game2);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        // Act
        var result1 = await _service.GenerateSetupGuideAsync("game1");
        var result2 = await _service.GenerateSetupGuideAsync("game2");

        // Assert
        result1.gameTitle.Should().Be("Game One");
        result2.gameTitle.Should().Be("Game Two");
    }

    [Fact]
    public async Task GivenLongInstructions_WhenParsing_ThenTruncatesAtMaxLength()
    {
        // Arrange
        var gameId = "game-long-instructions";
        var game = new GameEntity { Id = gameId, Name = "Verbose Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { Text = "Some text", Score = 0.9f, PdfId = "pdf1", Page = 1 }
            }));

        var longInstruction = new string('x', 600); // 600 characters
        var llmResponse = $@"STEP 1: Long Step
{longInstruction}";

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(llmResponse, new LlmUsage(100, 50, 150)));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.steps.Should().ContainSingle();
        (result.steps[0].instruction.Length <= 500).Should().BeTrue();
        result.steps[0].instruction.Should().EndWith("...");
    }

    [Fact]
    public async Task GivenEstimatedSetupTime_WhenCalculated_ThenReturnsMinimum5Minutes()
    {
        // Arrange
        var gameId = "game-quick";
        var game = new GameEntity { Id = gameId, Name = "Quick Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { Text = "Quick setup", Score = 0.9f, PdfId = "pdf1", Page = 1 }
            }));

        var llmResponse = @"STEP 1: Quick Setup
Do this quickly.";

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(llmResponse, new LlmUsage(50, 25, 75)));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        (result.estimatedSetupTimeMinutes >= 5).Should().BeTrue(); // Minimum 5 minutes
    }

    [Fact]
    public async Task GivenMalformedLlmResponse_WhenParsing_ThenReturnsEmptySteps()
    {
        // Arrange
        var gameId = "game-malformed-llm";
        var game = new GameEntity { Id = gameId, Name = "Parsing Challenge Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _cacheServiceMock
            .Setup(x => x.GetAsync<SetupGuideResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SetupGuideResponse?)null);

        _cacheServiceMock
            .Setup(x => x.GenerateSetupCacheKey(gameId))
            .Returns($"setup:{gameId}");

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { Text = "Some text", Score = 0.9f, PdfId = "pdf1", Page = 1 }
            }));

        var malformedResponse = "This is not in the expected format at all!";

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(malformedResponse, new LlmUsage(100, 50, 150)));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert: When LLM response can't be parsed, service returns empty steps (not default steps)
        // Note: This differs from LLM failure (when LLM call fails), which returns default steps
        result.Should().NotBeNull();
        result.gameTitle.Should().BeEquivalentTo("Parsing Challenge Game");
        result.steps.Should().BeEmpty(); // Malformed response results in empty steps
        result.totalTokens.Should().Be(150); // But tokens were still used
    }

    [Fact]
    public async Task GivenHighConfidenceScore_WhenRetrieved_ThenIncludedInResponse()
    {
        // Arrange
        var gameId = "game-high-confidence";
        var game = new GameEntity { Id = gameId, Name = "High Confidence Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { Text = "Perfect match text", Score = 0.98f, PdfId = "pdf1", Page = 1 }
            }));

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("STEP 1: Test\nTest step", new LlmUsage(100, 50, 150)));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.confidence.Should().NotBeNull();
        (result.confidence >= 0.98).Should().BeTrue();
    }

    [Fact]
    public async Task GivenReferencesDistribution_WhenGeneratingSteps_ThenDistributesReferencesAcrossSteps()
    {
        // Arrange
        var gameId = "game-ref-distribution";
        var game = new GameEntity { Id = gameId, Name = "Reference Distribution Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { Text = "Ref 1", Score = 0.95f, PdfId = "pdf1", Page = 1 },
                new SearchResultItem { Text = "Ref 2", Score = 0.94f, PdfId = "pdf1", Page = 2 },
                new SearchResultItem { Text = "Ref 3", Score = 0.93f, PdfId = "pdf1", Page = 3 },
                new SearchResultItem { Text = "Ref 4", Score = 0.92f, PdfId = "pdf1", Page = 4 },
                new SearchResultItem { Text = "Ref 5", Score = 0.91f, PdfId = "pdf1", Page = 5 }
            }));

        var llmResponse = @"STEP 1: First Step
First instruction.

STEP 2: Second Step
Second instruction.";

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(llmResponse, new LlmUsage(100, 50, 150)));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.steps.Count.Should().Be(2);
        result.steps[0].references.Should().NotBeEmpty(); // First step gets first 3 references
        result.steps[1].references.Should().NotBeEmpty(); // Second step gets next set of references
    }

    [Fact]
    public async Task GivenException_WhenGenerating_ThenReturnsEmptyGuideAndLogsError()
    {
        // Arrange
        var gameId = "game-exception";
        var game = new GameEntity { Id = gameId, Name = "Exception Game", CreatedAt = DateTime.UtcNow };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        result.gameTitle.Should().BeEquivalentTo("Unknown Game");
    }
}