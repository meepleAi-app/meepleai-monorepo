using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Tests for MultiModelEvaluator.
/// Issue #4332: Multi-model evaluation with GPT-4 + Claude + DeepSeek consensus.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class MultiModelEvaluatorTests
{
    private readonly Mock<ILlmService> _mockLlmService;
    private readonly Mock<IHybridCacheService> _mockCacheService;
    private readonly Mock<ILogger<MultiModelEvaluator>> _mockLogger;
    private readonly MultiModelEvaluator _evaluator;

    public MultiModelEvaluatorTests()
    {
        _mockLlmService = new Mock<ILlmService>();
        _mockCacheService = new Mock<IHybridCacheService>();
        _mockLogger = new Mock<ILogger<MultiModelEvaluator>>();

        _evaluator = new MultiModelEvaluator(
            _mockLlmService.Object,
            _mockCacheService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task EvaluateWithEnsembleAsync_AllModelsAgree_ReturnsHighConfidenceConsensus()
    {
        // Arrange
        var move = CreateTestMove();
        var state = CreateTestGameState();
        var playerColor = "white";

        // All models return similar scores (0.85, 0.83, 0.84) → high agreement
        SetupModelResponse("openai/gpt-4", 0.85, "Strong tactical advantage");
        SetupModelResponse("anthropic/claude-3.5-sonnet", 0.83, "Positional superiority");
        SetupModelResponse("deepseek/deepseek-chat", 0.84, "Material advantage");

        // Mock cache miss → factory execution
        _mockCacheService
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ConsensusResult>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<ConsensusResult>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, expiration, ct) => await factory(ct));

        // Act
        var result = await _evaluator.EvaluateWithEnsembleAsync(move, state, playerColor, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.Score, 0.83, 0.85);  // Weighted average within range
        Assert.True(result.Confidence >= 0.75, $"Expected confidence ≥0.75, got {result.Confidence}");
        result.Agreement.Should().Be("high");
        Assert.True(result.Variance < 0.01, $"Expected low variance, got {result.Variance}");
    }

    [Fact]
    public async Task EvaluateWithEnsembleAsync_ModelsDisagree_ReturnsLowerConfidence()
    {
        // Arrange
        var move = CreateTestMove();
        var state = CreateTestGameState();

        // Models disagree significantly (0.9, 0.3, 0.6) → low agreement
        SetupModelResponse("openai/gpt-4", 0.9, "Excellent move");
        SetupModelResponse("anthropic/claude-3.5-sonnet", 0.3, "Risky move");
        SetupModelResponse("deepseek/deepseek-chat", 0.6, "Moderate move");

        _mockCacheService
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ConsensusResult>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<ConsensusResult>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, expiration, ct) => await factory(ct));

        // Act
        var result = await _evaluator.EvaluateWithEnsembleAsync(move, state, "white", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Variance > 0.04, $"Expected high variance, got {result.Variance}");
        result.Agreement.Should().Be("low");
        Assert.True(result.Confidence <= 0.50, $"Expected low confidence, got {result.Confidence}");
    }

    [Fact]
    public async Task EvaluateWithEnsembleAsync_WeightedVoting_PrioritizesGPT4()
    {
        // Arrange
        var move = CreateTestMove();
        var state = CreateTestGameState();

        // GPT-4 (0.4 weight) = 0.9, Claude (0.35 weight) = 0.5, DeepSeek (0.25 weight) = 0.5
        // Weighted: (0.9*0.4) + (0.5*0.35) + (0.5*0.25) = 0.36 + 0.175 + 0.125 = 0.66
        SetupModelResponse("openai/gpt-4", 0.9, "GPT-4 analysis");
        SetupModelResponse("anthropic/claude-3.5-sonnet", 0.5, "Claude analysis");
        SetupModelResponse("deepseek/deepseek-chat", 0.5, "DeepSeek analysis");

        _mockCacheService
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ConsensusResult>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<ConsensusResult>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, expiration, ct) => await factory(ct));

        // Act
        var result = await _evaluator.EvaluateWithEnsembleAsync(move, state, "white", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        // Weighted score should be closer to GPT-4's 0.9 than simple average 0.63
        Assert.InRange(result.Score, 0.64, 0.68);
    }

    [Fact]
    public async Task EvaluateWithEnsembleAsync_AllModelsFail_ReturnsFallbackResult()
    {
        // Arrange
        var move = CreateTestMove();
        var state = CreateTestGameState();

        _mockCacheService
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ConsensusResult>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<ConsensusResult>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, expiration, ct) => await factory(ct));

        // All models fail
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("API error"));

        // Act
        var result = await _evaluator.EvaluateWithEnsembleAsync(move, state, "white", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.3, result.Confidence);  // Fallback confidence
        result.Agreement.Should().Be("none");
        result.Reasoning.Should().Contain("Ensemble unavailable");
    }

    [Fact]
    public async Task EvaluateWithEnsembleAsync_UsesCaching_AvoidsDuplicateCalls()
    {
        // Arrange
        var move = CreateTestMove();
        var state = CreateTestGameState();
        var cachedResult = new ConsensusResult
        {
            Score = 0.88,
            Confidence = 0.95,
            Agreement = "high",
            Reasoning = "Cached evaluation",
            Pros = new List<string> { "Cached advantage" },
            Cons = new List<string>(),
            ExpectedOutcome = "Cached outcome",
            Variance = 0.005
        };

        // Mock cache hit → factory NOT executed
        _mockCacheService
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ConsensusResult>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResult);

        // Act
        var result = await _evaluator.EvaluateWithEnsembleAsync(move, state, "white", TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(cachedResult);
        result.Reasoning.Should().Be("Cached evaluation");

        // Verify LLM service was NOT called (cache hit)
        _mockLlmService.Verify(
            s => s.GenerateCompletionWithModelAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateWithEnsembleAsync_PartialFailure_UsesSuccessfulModels()
    {
        // Arrange
        var move = CreateTestMove();
        var state = CreateTestGameState();

        // GPT-4 succeeds, Claude fails, DeepSeek succeeds
        SetupModelResponse("openai/gpt-4", 0.85, "Good move");
        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                "anthropic/claude-3.5-sonnet",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Claude API down"));
        SetupModelResponse("deepseek/deepseek-chat", 0.80, "Solid move");

        _mockCacheService
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<ConsensusResult>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<ConsensusResult>>, string[], TimeSpan, CancellationToken>(
                async (key, factory, tags, expiration, ct) => await factory(ct));

        // Act
        var result = await _evaluator.EvaluateWithEnsembleAsync(move, state, "white", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        // Weighted with 2 models: (0.85*0.4 + 0.80*0.25) / (0.4 + 0.25) = 0.83
        Assert.InRange(result.Score, 0.82, 0.84);
    }

    // Helper methods
    private void SetupModelResponse(string modelId, double score, string reasoning)
    {
        // Issue #4332: JSON must match ModelEvaluation record (capitalized properties)
        var jsonResponse = $$"""
        {
            "Score": {{score.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}},
            "Reasoning": "{{reasoning}}",
            "Pros": ["Advantage 1", "Advantage 2"],
            "Cons": ["Risk 1"],
            "ExpectedOutcome": "{{reasoning}}"
        }
        """;

        _mockLlmService
            .Setup(s => s.GenerateCompletionWithModelAsync(
                modelId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(jsonResponse));
    }

    private static CandidateMove CreateTestMove()
    {
        var pawn = ChessPiece.Create("Pawn", "White", "e2");
        var from = ChessPosition.FromNotation("e2");
        var to = ChessPosition.FromNotation("e4");
        var score = MoveScore.Create(
            material: 0,
            positional: 0.2,
            tactical: 0.1,
            development: 0.1);

        return CandidateMove.Create(
            piece: pawn,
            from: from,
            to: to,
            capturedPiece: null,
            score: score,
            priority: MovePriority.Medium);
    }

    private static ParsedGameState CreateTestGameState()
    {
        // Use parser to create valid state
        var parser = new ChessFENParser();
        return parser.Parse("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
    }
}
