using Api.Models;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// CHESS-04: Unit tests for chess conversational agent service
/// Tests question answering, FEN validation, move parsing, and caching
/// ADMIN-01 Phase 3: Updated with IPromptTemplateService and IConfiguration mocks
/// </summary>
public class ChessAgentServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<IChessKnowledgeService> _chessKnowledgeMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IAiResponseCacheService> _cacheMock;
    private readonly Mock<IPromptTemplateService> _promptTemplateMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<ChessAgentService>> _loggerMock;
    private readonly ChessAgentService _service;

    public ChessAgentServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _chessKnowledgeMock = new Mock<IChessKnowledgeService>();
        _llmServiceMock = new Mock<ILlmService>();
        _cacheMock = new Mock<IAiResponseCacheService>();
        _promptTemplateMock = new Mock<IPromptTemplateService>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<ChessAgentService>>();

        // ADMIN-01 Phase 3: Setup feature flag to use fallback (default behavior)
        // Mock IConfigurationSection for GetValue<bool> to work correctly
        var mockSection = new Mock<IConfigurationSection>();
        mockSection.Setup(s => s.Value).Returns("false");
        _configurationMock.Setup(c => c.GetSection("Features:PromptDatabase")).Returns(mockSection.Object);

        _service = new ChessAgentService(
            _chessKnowledgeMock.Object,
            _llmServiceMock.Object,
            _cacheMock.Object,
            _promptTemplateMock.Object,
            _configurationMock.Object,
            _loggerMock.Object
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
        result.suggestedMoves.Should().BeEmpty();
        result.sources.Should().BeEmpty();
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
        result.suggestedMoves.Should().BeEmpty();
        result.sources.Should().BeEmpty();
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

        _cacheMock
            .Setup(c => c.GenerateQaCacheKey("chess", "How does the knight move?|"))
            .Returns("cache_key_123");

        _cacheMock
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
        _chessKnowledgeMock.Verify(
            k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _llmServiceMock.Verify(
            l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AskAsync_WithNoCachedResponse_CachesNewResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("What is castling?");
        SetupSuccessfulChessAgentFlow("Castling is a special move...", "What is castling?");

        _cacheMock
            .Setup(c => c.GenerateQaCacheKey("chess", "What is castling?|"))
            .Returns("cache_key_456");

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>("cache_key_456", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        result.Should().NotBeNull();
        Assert.Contains("Castling", result.answer);

        // Should cache the response with 24h TTL
        _cacheMock.Verify(
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

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().NotBeEmpty();
        // Should not log validation errors
        _loggerMock.Verify(
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

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        result.Should().NotBeNull();
        // Should log warning about invalid FEN
        _loggerMock.Verify(
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

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        _chessKnowledgeMock
            .Setup(k => k.SearchChessKnowledgeAsync("Unknown chess topic", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("No results found"));

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("I don't have enough information to answer that question about chess.", result.answer);
        result.sources.Should().BeEmpty();
        result.suggestedMoves.Should().BeEmpty();

        // Should not call LLM service
        _llmServiceMock.Verify(
            l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AskAsync_WhenKnowledgeSearchReturnsNoResults_ReturnsEmptyResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("Obscure chess rule");

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        _chessKnowledgeMock
            .Setup(k => k.SearchChessKnowledgeAsync("Obscure chess rule", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("I don't have enough information to answer that question about chess.", result.answer);
        result.sources.Should().BeEmpty();
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

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        // Should search with enhanced query including position-related keywords
        _chessKnowledgeMock.Verify(
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

        _cacheMock
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

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        result.analysis.Should().NotBeNull();
        Assert.Equal(fenPosition, result.analysis!.fenPosition);
        result.analysis.evaluationSummary.Should().NotBeNull();
        Assert.Contains("advantage", result.analysis.evaluationSummary, StringComparison.OrdinalIgnoreCase);
        result.analysis.keyConsiderations.Should().NotBeEmpty();
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

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        result.analysis.Should().BeNull();
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

        _cacheMock
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

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        result.confidence.Should().NotBeNull();
        Assert.Equal(0.92, result.confidence.Value, precision: 2);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task AskAsync_WhenLlmServiceFails_ReturnsEmptyResponse()
    {
        // Arrange
        var request = new ChessAgentRequest("What is a fork?");

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        _chessKnowledgeMock
            .Setup(k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Text = "Fork is a tactic", Score = 0.9f, Page = 10, ChunkIndex = 1 }
            }));

        _llmServiceMock
            .Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("LLM service unavailable"));

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("Unable to generate answer.", result.answer);
        Assert.Single(result.sources); // Should still include sources from knowledge base
        Assert.Equal("Fork is a tactic", result.sources[0].text);

        // Should log error
        _loggerMock.Verify(
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

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache service error"));

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        Assert.Equal("An error occurred while processing your question.", result.answer);
        result.sources.Should().BeEmpty();
        result.suggestedMoves.Should().BeEmpty();

        // Should log exception
        _loggerMock.Verify(
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

        _cacheMock
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

        _cacheMock
            .Setup(c => c.GetAsync<ChessAgentResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChessAgentResponse?)null);

        // Act
        var result = await _service.AskAsync(request);

        // Assert
        result.metadata.Should().NotBeNull();
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
        _chessKnowledgeMock
            .Setup(k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        _llmServiceMock
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

        _chessKnowledgeMock
            .Setup(k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        _llmServiceMock
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

        _chessKnowledgeMock
            .Setup(k => k.SearchChessKnowledgeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        _llmServiceMock
            .Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                llmResponse,
                new LlmUsage(100, 50, 150),
                metadata
            ));
    }

    #endregion
}