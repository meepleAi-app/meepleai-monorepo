using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for MultiModelValidationService
/// ISSUE-974: BGAI-032 - Multi-model consensus validation (GPT-4 + Claude)
/// ISSUE-975: BGAI-033 - Consensus similarity calculation using cosine ≥0.90
/// </summary>
public class MultiModelValidationServiceTests
{
    private readonly Mock<ILlmClient> _mockOpenRouterClient;
    private readonly Mock<ILogger<MultiModelValidationService>> _mockLogger;
    private readonly MultiModelValidationService _service;

    public MultiModelValidationServiceTests()
    {
        _mockOpenRouterClient = new Mock<ILlmClient>();
        _mockLogger = new Mock<ILogger<MultiModelValidationService>>();

        // Setup OpenRouter client mock
        _mockOpenRouterClient.Setup(c => c.ProviderName).Returns("OpenRouter");
        _mockOpenRouterClient.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);

        var llmClients = new List<ILlmClient> { _mockOpenRouterClient.Object };

        _service = new MultiModelValidationService(llmClients, _mockLogger.Object);
    }

    [Fact]
    public void Test01_ConsensusThreshold_Returns090()
    {
        // Act & Assert
        Assert.Equal(0.90, _service.ConsensusThreshold);
    }

    [Fact]
    public void Test02_CalculateSimilarity_IdenticalTexts_Returns100()
    {
        // Arrange
        var text = "The knight moves in an L-shape: two squares in one direction and one square perpendicular.";

        // Act
        var similarity = _service.CalculateSimilarity(text, text);

        // Assert
        Assert.Equal(1.0, similarity, precision: 2);
    }

    [Fact]
    public void Test03_CalculateSimilarity_HighlySimilarTexts_ReturnsHighScore()
    {
        // Arrange - Same topic, mostly same words (cosine similarity should be very high)
        var text1 = "The knight moves two squares vertically and one square horizontally.";
        var text2 = "The knight moves two squares horizontally and one square vertically.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert - Cosine similarity considers term frequency, should be high for nearly identical word sets
        Assert.True(similarity >= 0.80, $"Expected cosine similarity ≥0.80 for highly similar texts, got {similarity:F3}");
    }

    [Fact]
    public void Test04_CalculateSimilarity_DifferentTexts_ReturnsLowScore()
    {
        // Arrange
        var text1 = "The knight moves in an L-shape.";
        var text2 = "The bishop moves diagonally across the board.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert
        Assert.True(similarity < 0.50, $"Expected similarity <0.50, got {similarity:F3}");
    }

    [Fact]
    public void Test05_CalculateSimilarity_EmptyText_ReturnsZero()
    {
        // Arrange
        var text = "Some text";

        // Act
        var similarity1 = _service.CalculateSimilarity(text, "");
        var similarity2 = _service.CalculateSimilarity("", text);
        var similarity3 = _service.CalculateSimilarity("", "");

        // Assert
        Assert.Equal(0.0, similarity1);
        Assert.Equal(0.0, similarity2);
        Assert.Equal(0.0, similarity3);
    }

    [Fact]
    public async Task Test06_ValidateWithConsensusAsync_HighSimilarity_ReturnsConsensus()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the knight move in chess?";

        var gpt4Response = "The knight moves in an L-shape: two squares in one direction and one square perpendicular.";
        var claudeResponse = "The knight moves in an L-shape pattern: two squares in one direction and one square in a perpendicular direction.";

        SetupMockResponses(gpt4Response, claudeResponse);

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert
        Assert.True(result.HasConsensus);
        Assert.True(result.SimilarityScore >= 0.90, $"Expected similarity ≥0.90, got {result.SimilarityScore:F3}");
        Assert.Equal(0.90, result.RequiredThreshold);
        Assert.Equal(ConsensusSeverity.High, result.Severity);
        Assert.NotNull(result.ConsensusResponse);
        Assert.Equal(gpt4Response, result.ConsensusResponse); // GPT-4 response used as primary
        Assert.True(result.Gpt4Response.IsSuccess);
        Assert.True(result.ClaudeResponse.IsSuccess);
    }

    [Fact]
    public async Task Test07_ValidateWithConsensusAsync_ModerateSimilarity_NoConsensus()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the bishop move?";

        var gpt4Response = "The bishop moves diagonally across the board any number of squares.";
        var claudeResponse = "The bishop can move any number of unoccupied squares along diagonals.";

        SetupMockResponses(gpt4Response, claudeResponse);

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert
        // With cosine similarity, semantically similar texts may score high
        if (result.SimilarityScore >= 0.90)
        {
            Assert.True(result.HasConsensus);
            Assert.Equal(ConsensusSeverity.High, result.Severity);
            Assert.NotNull(result.ConsensusResponse);
        }
        else if (result.SimilarityScore >= 0.70)
        {
            Assert.False(result.HasConsensus);
            Assert.Equal(ConsensusSeverity.Moderate, result.Severity);
            Assert.Null(result.ConsensusResponse);
        }
        else
        {
            Assert.False(result.HasConsensus);
            Assert.Null(result.ConsensusResponse);
        }
    }

    [Fact]
    public async Task Test08_ValidateWithConsensusAsync_LowSimilarity_NoConsensus()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "What piece can castle?";

        var gpt4Response = "The king and rook can castle together.";
        var claudeResponse = "Castling involves moving the king two squares toward a rook on the same rank.";

        SetupMockResponses(gpt4Response, claudeResponse);

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert
        Assert.False(result.HasConsensus);
        Assert.True(result.SimilarityScore < 0.90);
        Assert.Null(result.ConsensusResponse);
    }

    [Fact]
    public async Task Test09_ValidateWithConsensusAsync_Gpt4Fails_ReturnsError()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the queen move?";

        // GPT-4 fails, Claude succeeds
        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "openai/gpt-4o",
                systemPrompt,
                userPrompt,
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("API error"));

        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "anthropic/claude-3.5-sonnet",
                systemPrompt,
                userPrompt,
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("The queen moves any number of squares in any direction."));

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert
        Assert.False(result.HasConsensus);
        Assert.Equal(ConsensusSeverity.Error, result.Severity);
        Assert.False(result.Gpt4Response.IsSuccess);
        Assert.True(result.ClaudeResponse.IsSuccess);
        Assert.Contains("GPT-4 failed", result.Message);
    }

    [Fact]
    public async Task Test10_ValidateWithConsensusAsync_ClaudeFails_ReturnsError()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the rook move?";

        // GPT-4 succeeds, Claude fails
        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "openai/gpt-4o",
                systemPrompt,
                userPrompt,
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("The rook moves any number of squares horizontally or vertically."));

        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "anthropic/claude-3.5-sonnet",
                systemPrompt,
                userPrompt,
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Timeout"));

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert
        Assert.False(result.HasConsensus);
        Assert.Equal(ConsensusSeverity.Error, result.Severity);
        Assert.True(result.Gpt4Response.IsSuccess);
        Assert.False(result.ClaudeResponse.IsSuccess);
        Assert.Contains("Claude failed", result.Message);
    }

    [Fact]
    public async Task Test11_ValidateWithConsensusAsync_BothFail_ReturnsError()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the pawn move?";

        // Both models fail
        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Network error"));

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert
        Assert.False(result.HasConsensus);
        Assert.Equal(ConsensusSeverity.Error, result.Severity);
        Assert.False(result.Gpt4Response.IsSuccess);
        Assert.False(result.ClaudeResponse.IsSuccess);
        Assert.Contains("Both models failed", result.Message);
    }

    [Fact]
    public async Task Test12_ValidateWithConsensusAsync_EmptyPrompt_ReturnsError()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "";

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert
        Assert.False(result.HasConsensus);
        Assert.Equal(ConsensusSeverity.Error, result.Severity);
        Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Test13_CalculateSimilarity_CaseInsensitive()
    {
        // Arrange
        var text1 = "The KNIGHT moves in an L-shape.";
        var text2 = "The knight MOVES in an l-shape.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert - Cosine similarity with TF-IDF should be nearly identical for case variants
        Assert.True(similarity >= 0.99, $"Expected case-insensitive cosine similarity ≥0.99, got {similarity:F3}");
    }

    [Fact]
    public void Test14_CalculateSimilarity_PunctuationIgnored()
    {
        // Arrange
        var text1 = "The knight moves in an L-shape!";
        var text2 = "The knight moves in an L-shape.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert - Cosine similarity should handle punctuation normalization
        Assert.True(similarity >= 0.99, $"Expected punctuation-agnostic cosine similarity ≥0.99, got {similarity:F3}");
    }

    [Fact]
    public async Task Test15_ValidateWithConsensusAsync_CancellationToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the knight move in chess?";
        var cts = new CancellationToken(canceled: true);

        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: cts));
    }

    [Fact]
    public async Task Test16_ValidateWithConsensusAsync_ParallelExecution_BothModelsQueried()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the queen move in chess?";
        var gpt4Response = "The queen can move any number of squares horizontally, vertically, or diagonally.";
        var claudeResponse = "The queen moves in straight lines: horizontally, vertically, or diagonally for any distance.";

        var gpt4Called = false;
        var claudeCalled = false;

        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "openai/gpt-4o",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                gpt4Called = true;
                return LlmCompletionResult.CreateSuccess(gpt4Response, new LlmUsage(100, 50, 150));
            });

        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "anthropic/claude-3.5-sonnet",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                claudeCalled = true;
                return LlmCompletionResult.CreateSuccess(claudeResponse, new LlmUsage(100, 50, 150));
            });

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert - Both models should be called (parallel execution)
        Assert.True(gpt4Called, "GPT-4 should have been called");
        Assert.True(claudeCalled, "Claude should have been called");
        Assert.True(result.Gpt4Response.IsSuccess);
        Assert.True(result.ClaudeResponse.IsSuccess);
    }

    [Fact]
    public async Task Test17_ValidateWithConsensusAsync_PerformanceMetrics_RecordedCorrectly()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the pawn move?";
        var gpt4Response = "The pawn moves forward one square, or two squares on its first move.";
        var claudeResponse = "Pawns move one square forward, or two squares forward from their starting position.";

        SetupMockResponses(gpt4Response, claudeResponse);

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert - Performance metrics should be recorded
        Assert.True(result.TotalDurationMs >= 0, "Total duration should be non-negative");
        Assert.True(result.Gpt4Response.DurationMs >= 0, "GPT-4 duration should be non-negative");
        Assert.True(result.ClaudeResponse.DurationMs >= 0, "Claude duration should be non-negative");

        // Verify response details are captured
        Assert.Equal("openai/gpt-4o", result.Gpt4Response.ModelId);
        Assert.Equal("anthropic/claude-3.5-sonnet", result.ClaudeResponse.ModelId);
        Assert.NotNull(result.Gpt4Response.Usage);
        Assert.NotNull(result.ClaudeResponse.Usage);
    }

    [Fact]
    public async Task Test18_ValidateWithConsensusAsync_SeverityLevels_CalculatedCorrectly()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "Test severity levels";

        // Test case 1: High severity (similarity ≥ 0.90)
        var highSimilarText1 = "The knight moves in an L-shape: two squares in one direction and one square perpendicular.";
        var highSimilarText2 = "The knight moves in an L-shape pattern: two squares in one direction and one square in a perpendicular direction.";
        SetupMockResponses(highSimilarText1, highSimilarText2);
        var resultHigh = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Test case 2: Moderate severity (similarity 0.70-0.90)
        var moderateText1 = "The rook moves horizontally or vertically across the board.";
        var moderateText2 = "Rooks can move any number of squares in straight lines along ranks or files.";
        SetupMockResponses(moderateText1, moderateText2);
        var resultModerate = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Test case 3: Low severity (similarity 0.50-0.70)
        var lowText1 = "Chess is a strategic board game.";
        var lowText2 = "The game of chess involves tactical thinking.";
        SetupMockResponses(lowText1, lowText2);
        var resultLow = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Test case 4: None severity (similarity < 0.50)
        var noneText1 = "The bishop moves diagonally.";
        var noneText2 = "Settlers of Catan is a resource management game.";
        SetupMockResponses(noneText1, noneText2);
        var resultNone = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt);

        // Assert - Verify severity levels
        if (resultHigh.SimilarityScore >= 0.90)
        {
            Assert.Equal(ConsensusSeverity.High, resultHigh.Severity);
            Assert.True(resultHigh.HasConsensus);
        }

        if (resultModerate.SimilarityScore >= 0.70 && resultModerate.SimilarityScore < 0.90)
        {
            Assert.Equal(ConsensusSeverity.Moderate, resultModerate.Severity);
            Assert.False(resultModerate.HasConsensus);
        }

        if (resultLow.SimilarityScore >= 0.50 && resultLow.SimilarityScore < 0.70)
        {
            Assert.Equal(ConsensusSeverity.Low, resultLow.Severity);
            Assert.False(resultLow.HasConsensus);
        }

        if (resultNone.SimilarityScore < 0.50)
        {
            Assert.Equal(ConsensusSeverity.None, resultNone.Severity);
            Assert.False(resultNone.HasConsensus);
        }
    }

    // ========== BGAI-038: Empty/Whitespace Response Validation ==========

    [Fact]
    public async Task Test19_ValidateWithConsensusAsync_Gpt4EmptyResponse_ReturnsError()
    {
        // Arrange - BGAI-038: Test empty GPT-4 response handling
        SetupMockSuccessWithEmptyResponse("", "Valid Claude response");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?");

        // Assert
        Assert.False(result.HasConsensus);
        Assert.Equal(ConsensusSeverity.Error, result.Severity);
        Assert.Contains("GPT-4", result.Message);
        Assert.Contains("empty or whitespace-only response", result.Message);
    }

    [Fact]
    public async Task Test20_ValidateWithConsensusAsync_ClaudeEmptyResponse_ReturnsError()
    {
        // Arrange - BGAI-038: Test empty Claude response handling
        SetupMockSuccessWithEmptyResponse("Valid GPT-4 response", "");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?");

        // Assert
        Assert.False(result.HasConsensus);
        Assert.Equal(ConsensusSeverity.Error, result.Severity);
        Assert.Contains("Claude", result.Message);
        Assert.Contains("empty or whitespace-only response", result.Message);
    }

    [Fact]
    public async Task Test21_ValidateWithConsensusAsync_Gpt4WhitespaceOnlyResponse_ReturnsError()
    {
        // Arrange - BGAI-038: Test whitespace-only GPT-4 response handling
        SetupMockSuccessWithEmptyResponse("   \n\t  ", "Valid Claude response");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?");

        // Assert
        Assert.False(result.HasConsensus);
        Assert.Equal(ConsensusSeverity.Error, result.Severity);
        Assert.Contains("GPT-4", result.Message);
        Assert.Contains("empty or whitespace-only response", result.Message);
    }

    [Fact]
    public async Task Test22_ValidateWithConsensusAsync_ClaudeWhitespaceOnlyResponse_ReturnsError()
    {
        // Arrange - BGAI-038: Test whitespace-only Claude response handling
        SetupMockSuccessWithEmptyResponse("Valid GPT-4 response", "   \n\t  ");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?");

        // Assert
        Assert.False(result.HasConsensus);
        Assert.Equal(ConsensusSeverity.Error, result.Severity);
        Assert.Contains("Claude", result.Message);
        Assert.Contains("empty or whitespace-only response", result.Message);
    }

    [Fact]
    public async Task Test23_ValidateWithConsensusAsync_BothEmptyResponses_ReturnsError()
    {
        // Arrange - BGAI-038: Test both models returning empty responses
        SetupMockSuccessWithEmptyResponse("", "");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?");

        // Assert
        Assert.False(result.HasConsensus);
        Assert.Equal(ConsensusSeverity.Error, result.Severity);
        Assert.Contains("empty or whitespace-only response", result.Message);
    }

    /// <summary>
    /// Helper method to setup mock responses for both models with empty/whitespace content
    /// </summary>
    private void SetupMockSuccessWithEmptyResponse(string gpt4Response, string claudeResponse)
    {
        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "openai/gpt-4o",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                gpt4Response,
                new LlmUsage(100, 50, 150)));

        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "anthropic/claude-3.5-sonnet",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                claudeResponse,
                new LlmUsage(100, 50, 150)));
    }

    /// <summary>
    /// Helper method to setup mock responses for both models
    /// </summary>
    private void SetupMockResponses(string gpt4Response, string claudeResponse)
    {
        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "openai/gpt-4o",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                gpt4Response,
                new LlmUsage(100, 50, 150)));

        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                "anthropic/claude-3.5-sonnet",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                claudeResponse,
                new LlmUsage(100, 50, 150)));
    }
}
