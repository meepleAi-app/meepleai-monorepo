using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class SetupGuideServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IQdrantService> _mockQdrantService;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IAiResponseCacheService> _mockCacheService;
    private readonly Mock<ILogger<SetupGuideService>> _mockLogger;
    private readonly SetupGuideService _service;

    public SetupGuideServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite($"DataSource=SetupGuideTest_{Guid.NewGuid()};Mode=Memory;Cache=Shared")
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.OpenConnection();
        _dbContext.Database.EnsureCreated();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockQdrantService = new Mock<IQdrantService>();
        _mockLlmService = new Mock<ILlmService>();
        _mockCacheService = new Mock<IAiResponseCacheService>();
        _mockLogger = new Mock<ILogger<SetupGuideService>>();

        _service = new SetupGuideService(
            _dbContext,
            _mockEmbeddingService.Object,
            _mockQdrantService.Object,
            _mockLlmService.Object,
            _mockCacheService.Object,
            _mockLogger.Object
        );
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GenerateSetupGuideAsync_WithNonExistentGame_ReturnsDefaultGuide()
    {
        // Arrange
        var gameId = "nonexistent";

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unknown Game", result.gameTitle);
        Assert.NotEmpty(result.steps);
        Assert.True(result.estimatedSetupTimeMinutes > 0);
    }

    [Fact]
    public async Task GenerateSetupGuideAsync_WithValidGame_ReturnsSetupGuide()
    {
        // Arrange
        var gameId = "game1";

        // Create test game
        var game = new GameEntity
        {
            Id = gameId,
            Name = "Test Board Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Mock embedding service to return empty results (no RAG data)
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = false,
                Embeddings = new List<float[]>()
            });

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Board Game", result.gameTitle);
        Assert.NotEmpty(result.steps);
        Assert.All(result.steps, step =>
        {
            Assert.True(step.stepNumber > 0);
            Assert.False(string.IsNullOrEmpty(step.title));
            Assert.False(string.IsNullOrEmpty(step.instruction));
            Assert.NotNull(step.references);
        });
    }

    [Fact]
    public async Task GenerateSetupGuideAsync_WithRAGData_ReturnsEnrichedSteps()
    {
        // Arrange
        var gameId = "game1";

        var game = new GameEntity
        {
            Id = gameId,
            Name = "Advanced Strategy Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        // Mock embedding service
        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }
            });

        // Mock Qdrant service to return setup instructions
        _mockQdrantService
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem
                {
                    Text = "Place the game board in the center. Each player takes a player board.",
                    Score = 0.95f,
                    PdfId = "pdf123",
                    Page = 2
                }
            }));

        // Mock LLM service to synthesize steps from RAG context
        var llmResponse = @"STEP 1: Setup Game Board
Place the game board in the center where all players can reach it.

STEP 2: Distribute Player Materials
Each player takes a player board and starting components.";

        _mockLlmService
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(llmResponse, new LlmUsage(100, 80, 180)));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Advanced Strategy Game", result.gameTitle);
        Assert.NotEmpty(result.steps);
        Assert.NotNull(result.confidence);
        Assert.Equal(180, result.totalTokens); // LLM was used
        Assert.Equal(2, result.steps.Count); // LLM generated 2 steps

        // Verify steps have references from RAG
        var stepsWithReferences = result.steps.Where(s => s.references.Count > 0).ToList();
        Assert.NotEmpty(stepsWithReferences);

        // Verify step structure
        var firstStep = result.steps.First();
        Assert.Equal(1, firstStep.stepNumber);
        Assert.Equal("Setup Game Board", firstStep.title);
        Assert.Contains("center", firstStep.instruction);
    }

    [Fact]
    public void SetupStep_HasCorrectStructure()
    {
        // Arrange
        var step = new SetupGuideStep(
            stepNumber: 1,
            title: "Prepare Components",
            instruction: "Sort all components by type",
            references: new List<Snippet>
            {
                new Snippet("Reference text", "PDF:123", 5, 0)
            },
            isOptional: false
        );

        // Assert
        Assert.Equal(1, step.stepNumber);
        Assert.Equal("Prepare Components", step.title);
        Assert.Equal("Sort all components by type", step.instruction);
        Assert.Single(step.references);
        Assert.False(step.isOptional);
    }

    [Fact]
    public async Task SetupGuideResponse_CalculatesEstimatedTime()
    {
        // Arrange
        var gameId = "game1";

        var game = new GameEntity
        {
            Id = gameId,
            Name = "Quick Game",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        _mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = false, Embeddings = new List<float[]>() });

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        Assert.True(result.estimatedSetupTimeMinutes > 0);
        // Default steps are 5, so estimated time should be around 10 minutes (2 min per step)
        Assert.Equal(10, result.estimatedSetupTimeMinutes);

        await Task.CompletedTask;
    }
}
