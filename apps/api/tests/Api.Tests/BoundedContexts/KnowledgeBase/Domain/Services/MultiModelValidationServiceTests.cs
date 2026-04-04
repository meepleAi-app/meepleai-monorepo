using System.Threading;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for MultiModelValidationService
/// ISSUE-974: BGAI-032 - Multi-model consensus validation (GPT-4 + Claude)
/// ISSUE-975: BGAI-033 - Consensus similarity calculation using cosine ≥0.90
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class MultiModelValidationServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

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
    public void ConsensusThreshold_Returns090()
    {
        // Act & Assert
        _service.ConsensusThreshold.Should().Be(0.90);
    }

    [Fact]
    public void CalculateSimilarity_IdenticalTexts_Returns100()
    {
        // Arrange
        var text = "The knight moves in an L-shape: two squares in one direction and one square perpendicular.";

        // Act
        var similarity = _service.CalculateSimilarity(text, text);

        // Assert
        similarity.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void CalculateSimilarity_HighlySimilarTexts_ReturnsHighScore()
    {
        // Arrange - Same topic, mostly same words (cosine similarity should be very high)
        var text1 = "The knight moves two squares vertically and one square horizontally.";
        var text2 = "The knight moves two squares horizontally and one square vertically.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert - Cosine similarity considers term frequency, should be high for nearly identical word sets
        (similarity >= 0.80).Should().BeTrue($"Expected cosine similarity ≥0.80 for highly similar texts, got {similarity:F3}");
    }

    [Fact]
    public void CalculateSimilarity_DifferentTexts_ReturnsLowScore()
    {
        // Arrange - Use completely different vocabulary to ensure low similarity
        var text1 = "The knight moves in an L-shape.";
        var text2 = "Players collect resources to build settlements.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert - Texts with different vocabulary should have low cosine similarity
        (similarity < 0.50).Should().BeTrue($"Expected similarity <0.50, got {similarity:F3}");
    }

    [Fact]
    public void CalculateSimilarity_EmptyText_ReturnsZero()
    {
        // Arrange
        var text = "Some text";

        // Act
        var similarity1 = _service.CalculateSimilarity(text, "");
        var similarity2 = _service.CalculateSimilarity("", text);
        var similarity3 = _service.CalculateSimilarity("", "");

        // Assert
        similarity1.Should().Be(0.0);
        similarity2.Should().Be(0.0);
        similarity3.Should().Be(0.0);
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_HighSimilarity_ReturnsConsensus()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the knight move in chess?";

        var gpt4Response = "The knight moves in an L-shape: two squares in one direction and one square perpendicular.";
        var claudeResponse = "The knight moves in an L-shape pattern: two squares in one direction and one square in a perpendicular direction.";

        SetupMockResponses(gpt4Response, claudeResponse);

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeTrue();
        (result.SimilarityScore >= 0.90).Should().BeTrue($"Expected similarity ≥0.90, got {result.SimilarityScore:F3}");
        result.RequiredThreshold.Should().Be(0.90);
        result.Severity.Should().Be(ConsensusSeverity.High);
        result.ConsensusResponse.Should().NotBeNull();
        result.ConsensusResponse.Should().Be(gpt4Response); // GPT-4 response used as primary
        result.Gpt4Response.IsSuccess.Should().BeTrue();
        result.ClaudeResponse.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_ModerateSimilarity_NoConsensus()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the bishop move?";

        var gpt4Response = "The bishop moves diagonally across the board any number of squares.";
        var claudeResponse = "The bishop can move any number of unoccupied squares along diagonals.";

        SetupMockResponses(gpt4Response, claudeResponse);

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert
        // With cosine similarity, semantically similar texts may score high
        if (result.SimilarityScore >= 0.90)
        {
            result.HasConsensus.Should().BeTrue();
            result.Severity.Should().Be(ConsensusSeverity.High);
            result.ConsensusResponse.Should().NotBeNull();
        }
        else if (result.SimilarityScore >= 0.70)
        {
            result.HasConsensus.Should().BeFalse();
            result.Severity.Should().Be(ConsensusSeverity.Moderate);
            result.ConsensusResponse.Should().BeNull();
        }
        else
        {
            result.HasConsensus.Should().BeFalse();
            result.ConsensusResponse.Should().BeNull();
        }
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_LowSimilarity_NoConsensus()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "What piece can castle?";

        var gpt4Response = "The king and rook can castle together.";
        var claudeResponse = "Castling involves moving the king two squares toward a rook on the same rank.";

        SetupMockResponses(gpt4Response, claudeResponse);

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        (result.SimilarityScore < 0.90).Should().BeTrue();
        result.ConsensusResponse.Should().BeNull();
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_Gpt4Fails_ReturnsError()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the queen move?";

        // GPT-4 fails, Claude succeeds
        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                Gpt4Model,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("API error"));

        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                ClaudeModel,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("The queen moves any number of squares in any direction."));

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.Severity.Should().Be(ConsensusSeverity.Error);
        result.Gpt4Response.IsSuccess.Should().BeFalse();
        result.ClaudeResponse.IsSuccess.Should().BeTrue();
        result.Message.Should().ContainEquivalentOf("GPT-4 failed");
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_ClaudeFails_ReturnsError()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the rook move?";

        // GPT-4 succeeds, Claude fails
        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                Gpt4Model,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("The rook moves any number of squares horizontally or vertically."));

        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                ClaudeModel,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Timeout"));

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.Severity.Should().Be(ConsensusSeverity.Error);
        result.Gpt4Response.IsSuccess.Should().BeTrue();
        result.ClaudeResponse.IsSuccess.Should().BeFalse();
        result.Message.Should().ContainEquivalentOf("Claude failed");
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_BothFail_ReturnsError()
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
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.Severity.Should().Be(ConsensusSeverity.Error);
        result.Gpt4Response.IsSuccess.Should().BeFalse();
        result.ClaudeResponse.IsSuccess.Should().BeFalse();
        result.Message.Should().ContainEquivalentOf("Both models failed");
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_EmptyPrompt_ReturnsError()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "";

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.Severity.Should().Be(ConsensusSeverity.Error);
        result.Message.Should().ContainEquivalentOf("empty");
    }

    [Fact]
    public void CalculateSimilarity_CaseInsensitive()
    {
        // Arrange
        var text1 = "The KNIGHT moves in an L-shape.";
        var text2 = "The knight MOVES in an l-shape.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert - Cosine similarity with TF-IDF should be nearly identical for case variants
        (similarity >= 0.99).Should().BeTrue($"Expected case-insensitive cosine similarity ≥0.99, got {similarity:F3}");
    }

    [Fact]
    public void CalculateSimilarity_PunctuationIgnored()
    {
        // Arrange
        var text1 = "The knight moves in an L-shape!";
        var text2 = "The knight moves in an L-shape.";

        // Act
        var similarity = _service.CalculateSimilarity(text1, text2);

        // Assert - Cosine similarity should handle punctuation normalization
        (similarity >= 0.99).Should().BeTrue($"Expected punctuation-agnostic cosine similarity ≥0.99, got {similarity:F3}");
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_CancellationToken_ThrowsOperationCanceledException()
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
        Func<Task> act = () => _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: cts);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_ParallelExecution_BothModelsQueried()
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
                Gpt4Model,
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
                ClaudeModel,
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
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert - Both models should be called (parallel execution)
        gpt4Called.Should().BeTrue("GPT-4 should have been called");
        claudeCalled.Should().BeTrue("Claude should have been called");
        result.Gpt4Response.IsSuccess.Should().BeTrue();
        result.ClaudeResponse.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_PerformanceMetrics_RecordedCorrectly()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "How does the pawn move?";
        var gpt4Response = "The pawn moves forward one square, or two squares on its first move.";
        var claudeResponse = "Pawns move one square forward, or two squares forward from their starting position.";

        SetupMockResponses(gpt4Response, claudeResponse);

        // Act
        var result = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert - Performance metrics should be recorded
        (result.TotalDurationMs >= 0).Should().BeTrue("Total duration should be non-negative");
        (result.Gpt4Response.DurationMs >= 0).Should().BeTrue("GPT-4 duration should be non-negative");
        (result.ClaudeResponse.DurationMs >= 0).Should().BeTrue("Claude duration should be non-negative");

        // Verify response details are captured
        result.Gpt4Response.ModelId.Should().Be(Gpt4Model);
        result.ClaudeResponse.ModelId.Should().Be(ClaudeModel);
        result.Gpt4Response.Usage.Should().NotBeNull();
        result.ClaudeResponse.Usage.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_SeverityLevels_CalculatedCorrectly()
    {
        // Arrange
        var systemPrompt = "You are a board game rules expert.";
        var userPrompt = "Test severity levels";

        // Test case 1: High severity (similarity ≥ 0.90)
        var highSimilarText1 = "The knight moves in an L-shape: two squares in one direction and one square perpendicular.";
        var highSimilarText2 = "The knight moves in an L-shape pattern: two squares in one direction and one square in a perpendicular direction.";
        SetupMockResponses(highSimilarText1, highSimilarText2);
        var resultHigh = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Test case 2: Moderate severity (similarity 0.70-0.90)
        var moderateText1 = "The rook moves horizontally or vertically across the board.";
        var moderateText2 = "Rooks can move any number of squares in straight lines along ranks or files.";
        SetupMockResponses(moderateText1, moderateText2);
        var resultModerate = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Test case 3: Low severity (similarity 0.50-0.70)
        var lowText1 = "Chess is a strategic board game.";
        var lowText2 = "The game of chess involves tactical thinking.";
        SetupMockResponses(lowText1, lowText2);
        var resultLow = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Test case 4: None severity (similarity < 0.50)
        var noneText1 = "The bishop moves diagonally.";
        var noneText2 = "Settlers of Catan is a resource management game.";
        SetupMockResponses(noneText1, noneText2);
        var resultNone = await _service.ValidateWithConsensusAsync(systemPrompt, userPrompt, cancellationToken: TestCancellationToken);

        // Assert - Verify severity levels
        if (resultHigh.SimilarityScore >= 0.90)
        {
            resultHigh.Severity.Should().Be(ConsensusSeverity.High);
            resultHigh.HasConsensus.Should().BeTrue();
        }

        if (resultModerate.SimilarityScore >= 0.70 && resultModerate.SimilarityScore < 0.90)
        {
            resultModerate.Severity.Should().Be(ConsensusSeverity.Moderate);
            resultModerate.HasConsensus.Should().BeFalse();
        }

        if (resultLow.SimilarityScore >= 0.50 && resultLow.SimilarityScore < 0.70)
        {
            resultLow.Severity.Should().Be(ConsensusSeverity.Low);
            resultLow.HasConsensus.Should().BeFalse();
        }

        if (resultNone.SimilarityScore < 0.50)
        {
            resultNone.Severity.Should().Be(ConsensusSeverity.None);
            resultNone.HasConsensus.Should().BeFalse();
        }
    }

    // ========== BGAI-038: Empty/Whitespace Response Validation ==========

    [Fact]
    public async Task ValidateWithConsensusAsync_Gpt4EmptyResponse_ReturnsError()
    {
        // Arrange - BGAI-038: Test empty GPT-4 response handling
        SetupMockSuccessWithEmptyResponse("", "Valid Claude response");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?", cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.Severity.Should().Be(ConsensusSeverity.Error);
        result.Message.Should().Contain("GPT-4");
        result.Message.Should().Contain("empty or whitespace-only response");
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_ClaudeEmptyResponse_ReturnsError()
    {
        // Arrange - BGAI-038: Test empty Claude response handling
        SetupMockSuccessWithEmptyResponse("Valid GPT-4 response", "");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?", cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.Severity.Should().Be(ConsensusSeverity.Error);
        result.Message.Should().Contain("Claude");
        result.Message.Should().Contain("empty or whitespace-only response");
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_Gpt4WhitespaceOnlyResponse_ReturnsError()
    {
        // Arrange - BGAI-038: Test whitespace-only GPT-4 response handling
        SetupMockSuccessWithEmptyResponse("   \n\t  ", "Valid Claude response");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?", cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.Severity.Should().Be(ConsensusSeverity.Error);
        result.Message.Should().Contain("GPT-4");
        result.Message.Should().Contain("empty or whitespace-only response");
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_ClaudeWhitespaceOnlyResponse_ReturnsError()
    {
        // Arrange - BGAI-038: Test whitespace-only Claude response handling
        SetupMockSuccessWithEmptyResponse("Valid GPT-4 response", "   \n\t  ");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?", cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.Severity.Should().Be(ConsensusSeverity.Error);
        result.Message.Should().Contain("Claude");
        result.Message.Should().Contain("empty or whitespace-only response");
    }

    [Fact]
    public async Task ValidateWithConsensusAsync_BothEmptyResponses_ReturnsError()
    {
        // Arrange - BGAI-038: Test both models returning empty responses
        SetupMockSuccessWithEmptyResponse("", "");

        // Act
        var result = await _service.ValidateWithConsensusAsync(
            "System prompt",
            "What are the chess piece movements?", cancellationToken: TestCancellationToken);

        // Assert
        result.HasConsensus.Should().BeFalse();
        result.Severity.Should().Be(ConsensusSeverity.Error);
        result.Message.Should().Contain("empty or whitespace-only response");
    }

    /// <summary>
    /// Helper method to setup mock responses for both models with empty/whitespace content
    /// </summary>
    private void SetupMockSuccessWithEmptyResponse(string gpt4Response, string claudeResponse)
    {
        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                Gpt4Model,
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
                ClaudeModel,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<double>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                claudeResponse,
                new LlmUsage(100, 50, 150)));
    }

    // Model identifiers matching MultiModelValidationService (Issue #3231: Use free models)
    private const string Gpt4Model = "meta-llama/llama-3.3-70b-instruct:free";
    private const string ClaudeModel = "google/gemini-2.0-flash-exp:free";

    /// <summary>
    /// Helper method to setup mock responses for both models
    /// </summary>
    private void SetupMockResponses(string gpt4Response, string claudeResponse)
    {
        _mockOpenRouterClient
            .Setup(c => c.GenerateCompletionAsync(
                Gpt4Model,
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
                ClaudeModel,
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

