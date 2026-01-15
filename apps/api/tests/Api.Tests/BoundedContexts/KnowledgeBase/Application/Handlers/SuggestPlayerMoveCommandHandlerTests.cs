using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for SuggestPlayerMoveCommandHandler.
/// Issue #2421: Player Mode UI Controls - Backend endpoint tests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SuggestPlayerMoveCommandHandlerTests
{
    private readonly Mock<IAiResponseCacheService> _mockCache;
    private readonly Mock<ILogger<SuggestPlayerMoveCommandHandler>> _mockLogger;
    private readonly SuggestPlayerMoveCommandHandler _handler;

    public SuggestPlayerMoveCommandHandlerTests()
    {
        _mockCache = new Mock<IAiResponseCacheService>();
        _mockLogger = new Mock<ILogger<SuggestPlayerMoveCommandHandler>>();

        _handler = new SuggestPlayerMoveCommandHandler(
            _mockCache.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessfulResponse()
    {
        // Arrange
        var gameState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "players", new[] { new { id = "1", name = "Alice", score = 10 } } },
            { "currentTurn", 1 },
            { "phase", "resource-gathering" }
        };

        var command = new SuggestPlayerMoveCommand
        {
            GameId = "catan",
            GameState = gameState,
            Query = "Should I focus on wood or stone?"
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.primarySuggestion);
        Assert.NotEmpty(result.primarySuggestion.action);
        Assert.NotEmpty(result.primarySuggestion.rationale);
        Assert.True(result.overallConfidence >= 0.0 && result.overallConfidence <= 1.0);
        Assert.True(result.primarySuggestion.confidence >= 0.0 && result.primarySuggestion.confidence <= 1.0);
    }

    [Fact]
    public async Task Handle_WithInvalidQuery_ReturnsErrorResponse()
    {
        // Arrange
        var gameState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "players", new[] { new { id = "1", name = "Alice" } } }
        };

        // Query longer than 1000 characters (MaxQueryLength)
        var longQuery = new string('a', 1001);

        var command = new SuggestPlayerMoveCommand
        {
            GameId = "catan",
            GameState = gameState,
            Query = longQuery
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.0, result.overallConfidence);
        Assert.Equal("No suggestion available", result.primarySuggestion.action);
    }

    [Fact]
    public async Task Handle_WithCachedResponse_ReturnsCachedData()
    {
        // Arrange
        var gameState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "players", new[] { new { id = "1", name = "Alice" } } }
        };

        var command = new SuggestPlayerMoveCommand
        {
            GameId = "catan",
            GameState = gameState
        };

        var cachedResponse = new Api.Models.PlayerModeSuggestionResponse(
            primarySuggestion: new Api.Models.SuggestedMove(
                action: "Cached action",
                rationale: "From cache",
                expectedOutcome: null,
                confidence: 0.9
            ),
            alternativeMoves: new List<Api.Models.SuggestedMove>(),
            overallConfidence: 0.9,
            strategicContext: null,
            sources: new List<Api.Models.Snippet>(),
            promptTokens: 50,
            completionTokens: 25,
            totalTokens: 75,
            processingTimeMs: 100,
            metadata: null
        );

        _mockCache.Setup(c => c.GetAsync<Api.Models.PlayerModeSuggestionResponse>(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Cached action", result.primarySuggestion.action);
        Assert.Equal(0.9, result.overallConfidence);

        // Verify cache was checked
        _mockCache.Verify(c => c.GetAsync<Api.Models.PlayerModeSuggestionResponse>(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoCachedResponse_GeneratesNewSuggestion()
    {
        // Arrange
        var gameState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "players", new[] { new { id = "1", name = "Alice" } } }
        };

        var command = new SuggestPlayerMoveCommand
        {
            GameId = "catan",
            GameState = gameState,
            Query = "What should I do?"
        };

        _mockCache.Setup(c => c.GetAsync<Api.Models.PlayerModeSuggestionResponse>(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.Models.PlayerModeSuggestionResponse?)null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.primarySuggestion.action);

        // Verify cache was checked and response was cached
        _mockCache.Verify(c => c.GetAsync<Api.Models.PlayerModeSuggestionResponse>(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<Api.Models.PlayerModeSuggestionResponse>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAlternativeMoves_ReturnsMultipleSuggestions()
    {
        // Arrange
        var gameState = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            { "players", new[] { new { id = "1", name = "Alice" } } }
        };

        var command = new SuggestPlayerMoveCommand
        {
            GameId = "catan",
            GameState = gameState
        };

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.alternativeMoves);
        Assert.True(result.alternativeMoves.Count > 0 && result.alternativeMoves.Count <= 3);

        // Verify all alternatives have valid confidence scores
        foreach (var alt in result.alternativeMoves)
        {
            Assert.True(alt.confidence >= 0.0 && alt.confidence <= 1.0);
            Assert.NotEmpty(alt.action);
            Assert.NotEmpty(alt.rationale);
        }
    }
}
