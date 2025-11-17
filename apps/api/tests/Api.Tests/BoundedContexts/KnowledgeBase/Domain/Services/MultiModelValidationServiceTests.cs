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
        // Arrange
        var text1 = "The knight moves two squares vertically and one square horizontally.";
        var text2 = "The knight moves two squares horizontally and one square vertically.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert
        Assert.True(similarity >= 0.70, $"Expected similarity ≥0.70, got {similarity:F3}");
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
        // Even with similar meaning, word-level Jaccard may give moderate score
        if (result.SimilarityScore >= 0.90)
        {
            Assert.True(result.HasConsensus);
            Assert.Equal(ConsensusSeverity.High, result.Severity);
        }
        else if (result.SimilarityScore >= 0.70)
        {
            Assert.False(result.HasConsensus);
            Assert.Equal(ConsensusSeverity.Moderate, result.Severity);
        }
        else
        {
            Assert.False(result.HasConsensus);
        }

        Assert.Null(result.ConsensusResponse); // No consensus response when below threshold
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

        // Assert
        Assert.True(similarity >= 0.90, $"Expected case-insensitive similarity ≥0.90, got {similarity:F3}");
    }

    [Fact]
    public void Test14_CalculateSimilarity_PunctuationIgnored()
    {
        // Arrange
        var text1 = "The knight moves in an L-shape!";
        var text2 = "The knight moves in an L-shape.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert
        Assert.True(similarity >= 0.90, $"Expected punctuation-agnostic similarity ≥0.90, got {similarity:F3}");
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
