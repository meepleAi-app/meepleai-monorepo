using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// CHESS-04: Unit tests for chess conversational agent service
/// Tests question answering, FEN validation, move parsing, and caching
/// </summary>
public class ChessAgentServiceTests
{
    private readonly Mock<IChessKnowledgeService> _mockChessKnowledge;
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IAiResponseCacheService> _mockCache;
    private readonly Mock<ILogger<ChessAgentService>> _mockLogger;
    private readonly ChessAgentService _service;

    public ChessAgentServiceTests()
    {
        _mockChessKnowledge = new Mock<IChessKnowledgeService>();
        _mockLlmService = new Mock<ILlmService>();
        _mockCache = new Mock<IAiResponseCacheService>();
        _mockLogger = new Mock<ILogger<ChessAgentService>>();

        _service = new ChessAgentService(
            _mockChessKnowledge.Object,
            _mockLlmService.Object,
            _mockCache.Object,
            _mockLogger.Object
        );
    }

    #region Basic Request Validation

    [Fact]
    public async Task AskAsync_WithEmptyQuestion_ReturnsEmptyResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("");

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("Please provide a question.", result.answer);
        Assert.Empty(result.suggestedMoves);
        Assert.Empty(result.sources);
        Assert.Equal(0, result.promptTokens);
    }

    [Fact]
    public async Task AskAsync_WithWhitespaceQuestion_ReturnsEmptyResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("   ");

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("Please provide a question.", result.answer);
        Assert.Empty(result.suggestedMoves);
        Assert.Empty(result.sources);
    }

    #endregion

    #region Caching Tests

    [Fact]
    public async Task AskAsync_WithCachedResponse_ReturnsCachedResult()
    {
        // Arrange
        var request = new ChessAgentRequest("How does the knight move?");
        var cachedResponse = new ChessAgentResponse(
            "Knights move in L-shape",
            null,
            Array.Empty<string>(),
            Array.Empty<Snippet>(),
            10,
            5,
            15
        );

        _mockCache
            .Setup(c => c.GenerateQaCacheKey("chess", "How does the knight move?|"))
            .Returns("cache_key_123");

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>("cache_key_123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("Knights move in L-shape", result.answer);
        Assert.Equal(10, result.promptTokens);
        Assert.Equal(5, result.completionTokens);
        Assert.Equal(15, result.totalTokens);

        // Should not call knowledge or LLM services
        _mockChessKnowledge.Verify(
            k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockLlmService.Verify(
            l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AskAsync_WithNoCachedResponse_CachesNewResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("What is castling?");
        SetupSuccessfulChessAgentFlow("Castling is a special move...", "What is castling?");

        _mockCache
            .Setup(c => c.GenerateQaCacheKey("chess", "What is castling?|"))
            .Returns("cache_key_456");

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>("cache_key_456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Castling", result.answer);

        // Should cache the response with 24h TTL
        _mockCache.Verify(
            c => c.SetAsync("cache_key_456", It.IsAny<ChessAgentResponse>(), 86400, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region FEN Validation Tests

    [Theory]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1")] // Starting position
    [InlineData("r1bqkb1r/pppp1ppp/2n2n2/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4")] // Italian Game
    [InlineData("8/8/8/4k3/8/8/8/4K3 w - - 0 1")] // King vs King
    public async Task AskAsync_WithValidFenPosition_ProcessesSuccessfully(string fenPosition)
    {
        // Arrange
        var request = new ChessAgentRequest("Analyze this position", fenPosition);
        SetupSuccessfulChessAgentFlow("Position is balanced", "Analyze this position", fenPosition);

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.answer);
        // Should not log validation errors
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Invalid FEN")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Theory]
    [InlineData("invalid")] // Not enough ranks
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP")] // Only 7 ranks
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR/8")] // 9 ranks
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNX w")] // Invalid character X
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBN w")] // Rank with 7 squares
    public async Task AskAsync_WithInvalidFenPosition_LogsWarningButContinues(string fenPosition)
    {
        // Arrange
        var request = new ChessAgentRequest("What's the best move?", fenPosition);
        SetupSuccessfulChessAgentFlow("Cannot analyze invalid position", "What's the best move?", fenPosition);

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.NotNull(result);
        // Should log warning about invalid FEN
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Invalid FEN position")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Knowledge Base Search Tests

    [Fact]
    public async Task AskAsync_WhenKnowledgeSearchFails_ReturnsEmptyResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("Unknown chess topic");

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        _mockChessKnowledge
            .Setup(k => k.SearchChessKnowledgeAsync("Unknown chess topic", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("No results found"));

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("I don't have enough information to answer that question about chess.", result.answer);
        Assert.Empty(result.sources);
        Assert.Empty(result.suggestedMoves);

        // Should not call LLM service
        _mockLlmService.Verify(
            l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AskAsync_WhenKnowledgeSearchReturnsNoResults_ReturnsEmptyResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("Obscure chess rule");

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        _mockChessKnowledge
            .Setup(k => k.SearchChessKnowledgeAsync("Obscure chess rule", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("I don't have enough information to answer that question about chess.", result.answer);
        Assert.Empty(result.sources);
    }

    [Fact]
    public async Task AskAsync_WithFenPosition_EnhancesSearchQuery()
    {
        // Arrange
        var request = new ChessAgentRequest(
            "What's the best move?",
            "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1"
        );
        SetupSuccessfulChessAgentFlow("1. d5: Controls center", "What's the best move?", request.fenPosition);

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        // Should search with enhanced query including position-related keywords
        _mockChessKnowledge.Verify(
            k => k.SearchChessKnowledgeAsync(
                "What's the best move? position analysis tactics strategy",
                5,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region LLM Response Parsing Tests

    [Fact]
    public async Task AskAsync_ParsesMoveNotationFromLlmResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("Suggest a move");
        var llmResponse = @"In this position, I recommend:
1. e4: Controls the center and opens lines for the bishop
2. Nf3: Develops a piece and controls central squares
3. d4: Another strong central pawn move";

        SetupSuccessfulChessAgentFlow(llmResponse, "Suggest a move");

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Contains(result.suggestedMoves, m => m.Contains("e4") && m.Contains("Controls the center"));
        Assert.Contains(result.suggestedMoves, m => m.Contains("Nf3") && m.Contains("Develops a piece"));
        Assert.Contains(result.suggestedMoves, m => m.Contains("d4") && m.Contains("central pawn"));
        Assert.True(result.suggestedMoves.Count >= 3);
    }

    [Fact]
    public async Task AskAsync_ExtractsPositionAnalysisWhenFenProvided()
    {
        // Arrange
        var fenPosition = "r1bqkb1r/pppp1ppp/2n2n2/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4";
        var request = new ChessAgentRequest("Analyze this position", fenPosition);
        var llmResponse = @"White has a slight advantage due to better development.
The central control is balanced. White should defend the e4 pawn and develop the queenside pieces.
Key considerations: king safety is good, development is ahead, threat of d4 push exists.";

        SetupSuccessfulChessAgentFlow(llmResponse, "Analyze this position", fenPosition);

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.NotNull(result.analysis);
        Assert.Equal(fenPosition, result.analysis!.fenPosition);
        Assert.NotNull(result.analysis.evaluationSummary);
        Assert.Contains("advantage", result.analysis.evaluationSummary, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.analysis.keyConsiderations);
        Assert.Contains(result.analysis.keyConsiderations, c => c.Contains("king safety"));
        Assert.Contains(result.analysis.keyConsiderations, c => c.Contains("development"));
    }

    [Fact]
    public async Task AskAsync_WithoutFenPosition_DoesNotExtractAnalysis()
    {
        // Arrange
        var request = new ChessAgentRequest("What is the Italian Game?");
        var llmResponse = "The Italian Game is a chess opening that begins with 1.e4 e5 2.Nf3 Nc6 3.Bc4.";

        SetupSuccessfulChessAgentFlow(llmResponse, "What is the Italian Game?");

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Null(result.analysis);
        Assert.Contains("Italian Game", result.answer);
    }

    #endregion

    #region Source Citations Tests

    [Fact]
    public async Task AskAsync_IncludesSourceCitationsFromKnowledgeBase()
    {
        // Arrange
        var request = new ChessAgentRequest("How does castling work?");
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Castling involves the king and rook...", Score = 0.95f, Page = 5, ChunkIndex = 12 },
            new() { Text = "Castling requirements: king not in check...", Score = 0.90f, Page = 5, ChunkIndex = 13 },
            new() { Text = "Kingside castling notation: O-O", Score = 0.85f, Page = 6, ChunkIndex = 14 }
        };

        SetupChessAgentFlowWithCustomResults("Castling is a special move...", searchResults);

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal(3, result.sources.Count);
        Assert.Equal("ChessKnowledge:12", result.sources[0].source);
        Assert.Equal(5, result.sources[0].page);
        Assert.Equal("ChessKnowledge:13", result.sources[1].source);
        Assert.Equal("ChessKnowledge:14", result.sources[2].source);
        Assert.Equal(6, result.sources[2].page);
    }

    [Fact]
    public async Task AskAsync_SetsConfidenceFromTopSearchScore()
    {
        // Arrange
        var request = new ChessAgentRequest("What is en passant?");
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "En passant is a special pawn capture", Score = 0.92f, Page = 3, ChunkIndex = 5 },
            new() { Text = "En passant can only occur immediately after", Score = 0.88f, Page = 3, ChunkIndex = 6 }
        };

        SetupChessAgentFlowWithCustomResults("En passant is...", searchResults);

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.NotNull(result.confidence);
        Assert.Equal(0.92, result.confidence.Value, precision: 2);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task AskAsync_WhenLlmServiceFails_ReturnsEmptyResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("What is a fork?");

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        _mockChessKnowledge
            .Setup(k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Text = "Fork is a tactic", Score = 0.9f, Page = 10, ChunkIndex = 1 }
            }));

        _mockLlmService
            .Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("LLM service unavailable"));

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("Unable to generate answer.", result.answer);
        Assert.Single(result.sources); // Should still include sources from knowledge base
        Assert.Equal("Fork is a tactic", result.sources[0].text);

        // Should log error
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to generate LLM response")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task AskAsync_WhenExceptionThrown_ReturnsErrorResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("Test exception");

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache service error"));

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("An error occurred while processing your question.", result.answer);
        Assert.Empty(result.sources);
        Assert.Empty(result.suggestedMoves);

        // Should log exception
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Error during chess agent query")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Token Usage Tests

    [Fact]
    public async Task AskAsync_ReturnsTokenUsageFromLlm()
    {
        // Arrange
        var request = new ChessAgentRequest("Explain the Sicilian Defense");
        SetupSuccessfulChessAgentFlowWithTokens(
            "The Sicilian Defense is...",
            promptTokens: 150,
            completionTokens: 80,
            totalTokens: 230
        );

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal(150, result.promptTokens);
        Assert.Equal(80, result.completionTokens);
        Assert.Equal(230, result.totalTokens);
    }

    [Fact]
    public async Task AskAsync_IncludesLlmMetadataInResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("What is checkmate?");
        var metadata = new Dictionary<string, string>
        {
            ["model"] = "anthropic/claude-3.5-sonnet",
            ["finish_reason"] = "stop",
            ["response_id"] = "resp_abc123"
        };

        SetupSuccessfulChessAgentFlowWithMetadata("Checkmate occurs when...", metadata);

        _mockCache
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.NotNull(result.metadata);
        Assert.Equal("anthropic/claude-3.5-sonnet", result.metadata["model"]);
        Assert.Equal("stop", result.metadata["finish_reason"]);
        Assert.Equal("resp_abc123", result.metadata["response_id"]);
    }

    #endregion

    #region Helper Methods

    private void SetupSuccessfulChessAgentFlow(string llmResponse, string question, string? fenPosition = null)
    {
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Relevant chess knowledge", Score = 0.9f, Page = 1, ChunkIndex = 0 }
        };

        SetupChessAgentFlowWithCustomResults(llmResponse, searchResults);
    }

    private void SetupChessAgentFlowWithCustomResults(string llmResponse, List<SearchResultItem> searchResults)
    {
        _mockChessKnowledge
            .Setup(k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        _mockLlmService
            .Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                llmResponse,
                new LlmUsage(100, 50, 150),
                new Dictionary<string, string>
                {
                    ["model"] = "test-model",
                    ["finish_reason"] = "stop"
                }
            ));
    }

    private void SetupSuccessfulChessAgentFlowWithTokens(
        string llmResponse,
        int promptTokens,
        int completionTokens,
        int totalTokens)
    {
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Chess knowledge", Score = 0.9f, Page = 1, ChunkIndex = 0 }
        };

        _mockChessKnowledge
            .Setup(k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        _mockLlmService
            .Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                llmResponse,
                new LlmUsage(promptTokens, completionTokens, totalTokens),
                new Dictionary<string, string> { ["model"] = "test-model" }
            ));
    }

    private void SetupSuccessfulChessAgentFlowWithMetadata(
        string llmResponse,
        Dictionary<string, string> metadata)
    {
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Chess knowledge", Score = 0.9f, Page = 1, ChunkIndex = 0 }
        };

        _mockChessKnowledge
            .Setup(k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        _mockLlmService
            .Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                llmResponse,
                new LlmUsage(100, 50, 150),
                metadata
            ));
    }

    #endregion
}
