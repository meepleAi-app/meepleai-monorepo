using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Handlers;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for SuggestPlayerMoveCommandHandler.
/// Issue #2473: Production AI Implementation for Player Mode Move Suggestions.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SuggestPlayerMoveCommandHandlerTests
{
    private readonly Mock<IAiResponseCacheService> _mockCache;
    private readonly Mock<ILogger<SuggestPlayerMoveCommandHandler>> _mockLogger;
    private readonly Mock<IGameStateParser> _mockParser;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly SuggestPlayerMoveCommandHandler _handler;

    public SuggestPlayerMoveCommandHandlerTests()
    {
        _mockCache = new Mock<IAiResponseCacheService>();
        _mockLogger = new Mock<ILogger<SuggestPlayerMoveCommandHandler>>();
        _mockParser = new Mock<IGameStateParser>();
        _mockMediator = new Mock<IMediator>();
        _mockLlmService = new Mock<ILlmService>();

        _handler = new SuggestPlayerMoveCommandHandler(
            _mockCache.Object,
            _mockLogger.Object,
            _mockParser.Object,
            _mockMediator.Object,
            _mockLlmService.Object
        );
    }

    [Fact]
    public async Task Handle_WithCachedResponse_ShouldReturnCachedData()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var gameState = new Dictionary<string, object> { ["test"] = "value" };
        var command = new SuggestPlayerMoveCommand
        {
            GameId = gameId,
            GameState = gameState,
            Query = "What should I do?"
        };

        var cachedResponse = new PlayerModeSuggestionResponse(
            primarySuggestion: new SuggestedMove("Cached action", "Cached rationale", null, 0.9),
            alternativeMoves: new List<SuggestedMove>(),
            overallConfidence: 0.9,
            strategicContext: "Cached context",
            sources: new List<Snippet>(),
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: 150,
            processingTimeMs: 500,
            metadata: new Dictionary<string, object>()
        );

        _mockCache
            .Setup(c => c.GetAsync<PlayerModeSuggestionResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(cachedResponse);
        _mockParser.Verify(p => p.Parse(It.IsAny<IReadOnlyDictionary<string, object>>()), Times.Never);
        _mockMediator.Verify(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithInvalidGameState_ShouldReturnFallbackResponse()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var gameState = new Dictionary<string, object> { ["invalid"] = "data" };
        var command = new SuggestPlayerMoveCommand
        {
            GameId = gameId,
            GameState = gameState,
            Query = null
        };

        _mockCache
            .Setup(c => c.GetAsync<PlayerModeSuggestionResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerModeSuggestionResponse?)null);

        _mockParser
            .Setup(p => p.Parse(It.IsAny<IReadOnlyDictionary<string, object>>()))
            .Returns((ParsedGameState?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.primarySuggestion.action.Should().Be("No suggestion available");
        result.overallConfidence.Should().Be(0.0);
    }

    [Fact]
    public async Task Handle_WithValidStateAndNoRagResults_ShouldStillGenerateSuggestion()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var gameState = CreateValidGameState();
        var command = new SuggestPlayerMoveCommand
        {
            GameId = gameId,
            GameState = gameState,
            Query = "What should I build?"
        };

        var parsedState = CreateParsedGameState();
        var llmOutput = CreateValidLlmOutput();

        _mockCache
            .Setup(c => c.GetAsync<PlayerModeSuggestionResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerModeSuggestionResponse?)null);

        _mockParser
            .Setup(p => p.Parse(It.IsAny<IReadOnlyDictionary<string, object>>()))
            .Returns(parsedState);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResultDto>()); // No RAG results

        _mockLlmService
            .Setup(l => l.GenerateJsonAsync<LlmMoveGenerationOutput>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmOutput);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.primarySuggestion.action.Should().Be("Build a settlement");
        result.overallConfidence.Should().BeGreaterThan(0.0);
        result.sources.Should().BeEmpty(); // No RAG results
    }

    [Fact]
    public async Task Handle_WithRagAndLlmSuccess_ShouldCalculateCorrectConfidence()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var gameState = CreateValidGameState();
        var command = new SuggestPlayerMoveCommand
        {
            GameId = gameId,
            GameState = gameState,
            Query = null
        };

        var parsedState = CreateParsedGameState();
        var ragResults = CreateRagResults();
        var llmOutput = CreateValidLlmOutput();

        _mockCache
            .Setup(c => c.GetAsync<PlayerModeSuggestionResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerModeSuggestionResponse?)null);

        _mockParser
            .Setup(p => p.Parse(It.IsAny<IReadOnlyDictionary<string, object>>()))
            .Returns(parsedState);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<SearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ragResults);

        _mockLlmService
            .Setup(l => l.GenerateJsonAsync<LlmMoveGenerationOutput>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmOutput);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.overallConfidence.Should().BeGreaterThan(0.6);
        result.sources.Should().HaveCount(3);
        result.promptTokens.Should().BeGreaterThan(0);
        result.completionTokens.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Handle_ShouldCacheSuccessfulResponse()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var gameState = CreateValidGameState();
        var command = new SuggestPlayerMoveCommand
        {
            GameId = gameId,
            GameState = gameState,
            Query = null
        };

        var parsedState = CreateParsedGameState();
        var llmOutput = CreateValidLlmOutput();

        _mockCache
            .Setup(c => c.GetAsync<PlayerModeSuggestionResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerModeSuggestionResponse?)null);

        _mockParser
            .Setup(p => p.Parse(It.IsAny<IReadOnlyDictionary<string, object>>()))
            .Returns(parsedState);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResultDto>());

        _mockLlmService
            .Setup(l => l.GenerateJsonAsync<LlmMoveGenerationOutput>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmOutput);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockCache.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<PlayerModeSuggestionResponse>(),
                3600,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithLlmFailure_ShouldReturnFallback()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var gameState = CreateValidGameState();
        var command = new SuggestPlayerMoveCommand
        {
            GameId = gameId,
            GameState = gameState,
            Query = null
        };

        var parsedState = CreateParsedGameState();

        _mockCache
            .Setup(c => c.GetAsync<PlayerModeSuggestionResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PlayerModeSuggestionResponse?)null);

        _mockParser
            .Setup(p => p.Parse(It.IsAny<IReadOnlyDictionary<string, object>>()))
            .Returns(parsedState);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResultDto>());

        _mockLlmService
            .Setup(l => l.GenerateJsonAsync<LlmMoveGenerationOutput>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmMoveGenerationOutput?)null); // LLM failure

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.primarySuggestion.action.Should().Contain("Analyze current position");
        result.overallConfidence.Should().BeLessThan(0.6);
    }

    [Fact]
    public async Task Handle_WithInvalidQuery_ShouldReturnErrorResponse()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var gameState = CreateValidGameState();
        var longQuery = new string('a', 1001); // Exceeds MaxQueryLength

        var command = new SuggestPlayerMoveCommand
        {
            GameId = gameId,
            GameState = gameState,
            Query = longQuery
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.primarySuggestion.action.Should().Be("No suggestion available");
        result.overallConfidence.Should().Be(0.0);
    }

    // Helper methods
    private static Dictionary<string, object> CreateValidGameState()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["players"] = JsonSerializer.SerializeToElement(new[]
            {
                new { name = "Player1", resources = new { wood = 2 }, score = 3 }
            }),
            ["resources"] = new Dictionary<string, object> { ["wood"] = 5 },
            ["currentPhase"] = "main",
            ["currentTurn"] = 5
        };
    }

    private static ParsedGameState CreateParsedGameState()
    {
        var players = new List<PlayerState>
        {
            new("Player1", new Dictionary<string, object> { ["wood"] = 2 }, 3, null)
        };

        var resources = new Dictionary<string, object> { ["wood"] = 5 };

        return new ParsedGameState(
            players,
            resources,
            null,
            "main",
            5,
            0.8
        );
    }

    private static List<SearchResultDto> CreateRagResults()
    {
        return new List<SearchResultDto>
        {
            new("doc1", "Build settlements early for victory points", 1, 0.85, 1, "hybrid"),
            new("doc2", "Wood is essential for construction", 2, 0.78, 2, "hybrid"),
            new("doc3", "Consider trading with other players", 3, 0.72, 3, "hybrid")
        };
    }

    private static LlmMoveGenerationOutput CreateValidLlmOutput()
    {
        return new LlmMoveGenerationOutput
        {
            PrimarySuggestion = new PrimarySuggestion
            {
                Action = "Build a settlement",
                Rationale = "You have enough resources",
                ExpectedOutcome = "Gain 1 victory point",
                Confidence = 0.85
            },
            Alternatives = new List<AlternativeMove>
            {
                new()
                {
                    Action = "Build a road",
                    Rationale = "Expand territory",
                    ExpectedOutcome = "Connect settlements",
                    Confidence = 0.72
                }
            },
            StrategicContext = "Focus on early settlements"
        };
    }
}
