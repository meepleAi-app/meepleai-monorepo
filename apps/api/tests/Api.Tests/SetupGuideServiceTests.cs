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
/// ADMIN-01 Phase 3: Updated with IPromptTemplateService and IConfiguration mocks
/// </summary>
public class SetupGuideServiceTests : IDisposable
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

    public SetupGuideServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite($"DataSource=SetupGuideTest_{Guid.NewGuid()};Mode=Memory;Cache=Shared")
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
    public async Task GenerateSetupGuideAsync_WithNonExistentGame_ReturnsDefaultGuide()
    {
        // Arrange
        var gameId = "nonexistent";

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        Assert.Equal("Unknown Game", result.gameTitle);
        result.steps.Should().NotBeEmpty();
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
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = false,
                Embeddings = new List<float[]>()
            });

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        Assert.Equal("Test Board Game", result.gameTitle);
        result.steps.Should().NotBeEmpty();
        Assert.All(result.steps, step =>
        {
            Assert.True(step.stepNumber > 0);
            Assert.False(string.IsNullOrEmpty(step.title));
            Assert.False(string.IsNullOrEmpty(step.instruction));
            step.references.Should().NotBeNull();
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
        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }
            });

        // Mock Qdrant service to return setup instructions
        _qdrantServiceMock
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

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(llmResponse, new LlmUsage(100, 80, 180)));

        // Act
        var result = await _service.GenerateSetupGuideAsync(gameId);

        // Assert
        result.Should().NotBeNull();
        Assert.Equal("Advanced Strategy Game", result.gameTitle);
        result.steps.Should().NotBeEmpty();
        result.confidence.Should().NotBeNull();
        Assert.Equal(180, result.totalTokens); // LLM was used
        Assert.Equal(2, result.steps.Count); // LLM generated 2 steps

        // Verify steps have references from RAG
        var stepsWithReferences = result.steps.Where(s => s.references.Count > 0).ToList();
        stepsWithReferences.Should().NotBeEmpty();

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
                new Snippet("Reference text", "PDF:123", 5, 0, 0.85f)
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

        _embeddingServiceMock
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