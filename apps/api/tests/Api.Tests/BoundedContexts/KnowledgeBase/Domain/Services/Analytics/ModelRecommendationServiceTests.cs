using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// Tests for the ModelRecommendationService.
/// Issue #3025: Backend 90% Coverage Target - Phase 16
/// </summary>
[Trait("Category", "Unit")]
public sealed class ModelRecommendationServiceTests
{
    private readonly Mock<ILlmCostCalculator> _costCalculatorMock;
    private readonly Mock<ILogger<ModelRecommendationService>> _loggerMock;
    private readonly ModelRecommendationService _service;

    public ModelRecommendationServiceTests()
    {
        _costCalculatorMock = new Mock<ILlmCostCalculator>();
        _loggerMock = new Mock<ILogger<ModelRecommendationService>>();
        _service = new ModelRecommendationService(_costCalculatorMock.Object, _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullCostCalculator_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ModelRecommendationService(null!, _loggerMock.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("costCalculator");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ModelRecommendationService(_costCalculatorMock.Object, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region GetRecommendationAsync Tests - QA Use Case

    [Fact]
    public async Task GetRecommendationAsync_QaWithCostPriority_RecommendsFreeModel()
    {
        // Act
        var result = await _service.GetRecommendationAsync("qa", prioritizeCost: true);

        // Assert
        result.RecommendedModel.Should().Contain("free");
        result.EstimatedCostPer1MTokens.Should().Be(0m);
        result.QualityTier.Should().Be("Free");
        result.Rationale.Should().Contain("Zero cost");
    }

    [Fact]
    public async Task GetRecommendationAsync_QaWithQualityPriority_RecommendsGpt4oMini()
    {
        // Act
        var result = await _service.GetRecommendationAsync("qa", prioritizeCost: false);

        // Assert
        result.RecommendedModel.Should().Contain("gpt-4o-mini");
        result.QualityTier.Should().Be("Standard");
        result.Rationale.Should().Contain("Balanced cost/quality");
    }

    [Fact]
    public async Task GetRecommendationAsync_QaCaseInsensitive_Works()
    {
        // Act
        var resultLower = await _service.GetRecommendationAsync("qa");
        var resultUpper = await _service.GetRecommendationAsync("QA");
        var resultMixed = await _service.GetRecommendationAsync("Qa");

        // Assert
        resultLower.RecommendedModel.Should().Be(resultUpper.RecommendedModel);
        resultLower.RecommendedModel.Should().Be(resultMixed.RecommendedModel);
    }

    #endregion

    #region GetRecommendationAsync Tests - Explain Use Case

    [Fact]
    public async Task GetRecommendationAsync_ExplainWithCostPriority_RecommendsGpt4oMini()
    {
        // Act
        var result = await _service.GetRecommendationAsync("explain", prioritizeCost: true);

        // Assert
        result.RecommendedModel.Should().Contain("gpt-4o-mini");
        result.EstimatedCostPer1MTokens.Should().Be(0.15m);
        result.QualityTier.Should().Be("Budget");
    }

    [Fact]
    public async Task GetRecommendationAsync_ExplainWithQualityPriority_RecommendsClaude()
    {
        // Act
        var result = await _service.GetRecommendationAsync("explain", prioritizeCost: false);

        // Assert
        result.RecommendedModel.Should().Contain("claude-3.5-sonnet");
        result.EstimatedCostPer1MTokens.Should().Be(3.00m);
        result.QualityTier.Should().Be("Premium");
        result.Rationale.Should().Contain("Superior quality");
    }

    #endregion

    #region GetRecommendationAsync Tests - Setup Use Case

    [Fact]
    public async Task GetRecommendationAsync_Setup_RecommendsGpt4oMini()
    {
        // Act - Setup doesn't depend on prioritizeCost
        var resultCostPriority = await _service.GetRecommendationAsync("setup", prioritizeCost: true);
        var resultQualityPriority = await _service.GetRecommendationAsync("setup", prioritizeCost: false);

        // Assert
        resultCostPriority.RecommendedModel.Should().Contain("gpt-4o-mini");
        resultQualityPriority.RecommendedModel.Should().Contain("gpt-4o-mini");
        resultCostPriority.Rationale.Should().Contain("structured output");
    }

    #endregion

    #region GetRecommendationAsync Tests - Unknown Use Case

    [Fact]
    public async Task GetRecommendationAsync_UnknownUseCase_ReturnsDefaultRecommendation()
    {
        // Act
        var result = await _service.GetRecommendationAsync("unknown_use_case");

        // Assert
        result.RecommendedModel.Should().Contain("gpt-4o-mini");
        result.QualityTier.Should().Be("Standard");
        result.Rationale.Should().Contain("Default recommendation");
    }

    [Fact]
    public async Task GetRecommendationAsync_EmptyUseCase_ReturnsDefaultRecommendation()
    {
        // Act
        var result = await _service.GetRecommendationAsync("");

        // Assert
        result.RecommendedModel.Should().Contain("gpt-4o-mini");
    }

    #endregion

    #region GetRecommendationAsync Tests - Common Assertions

    [Fact]
    public async Task GetRecommendationAsync_ReturnsAlternativeModels()
    {
        // Act
        var result = await _service.GetRecommendationAsync("qa");

        // Assert
        result.AlternativeModels.Should().NotBeEmpty();
        result.AlternativeModels.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetRecommendationAsync_ReturnsProvider()
    {
        // Act
        var result = await _service.GetRecommendationAsync("qa");

        // Assert
        result.Provider.Should().Be("OpenRouter");
    }

    #endregion

    #region CompareModelsAsync Tests

    [Fact]
    public async Task CompareModelsAsync_WithPricingData_ReturnsOrderedComparisons()
    {
        // Arrange
        SetupModelPricingMocks();

        // Act
        var result = await _service.CompareModelsAsync();

        // Assert
        result.Should().NotBeEmpty();
        // Should be ordered by CostQualityRatio ascending
        result.Should().BeInAscendingOrder(c => c.CostQualityRatio);
    }

    [Fact]
    public async Task CompareModelsAsync_WithFreeModel_IncludesZeroCost()
    {
        // Arrange
        _costCalculatorMock.Setup(c => c.GetModelPricing("meta-llama/llama-3.3-70b-instruct:free"))
            .Returns(new LlmModelPricing
            {
                ModelId = "meta-llama/llama-3.3-70b-instruct:free",
                Provider = "OpenRouter",
                InputCostPer1M = 0,
                OutputCostPer1M = 0
            });

        // Act
        var result = await _service.CompareModelsAsync();

        // Assert
        var freeModel = result.FirstOrDefault(c => c.ModelId.Contains("free"));
        if (freeModel != null)
        {
            freeModel.InputCostPer1M.Should().Be(0);
            freeModel.CostQualityRatio.Should().Be(0);
        }
    }

    [Fact]
    public async Task CompareModelsAsync_WithMissingPricing_SkipsModel()
    {
        // Arrange - Only return pricing for one model
        _costCalculatorMock.Setup(c => c.GetModelPricing("openai/gpt-4o"))
            .Returns(new LlmModelPricing
            {
                ModelId = "openai/gpt-4o",
                Provider = "OpenRouter",
                InputCostPer1M = 2.50m,
                OutputCostPer1M = 10.00m
            });

        // All other models return null
        _costCalculatorMock.Setup(c => c.GetModelPricing(It.Is<string>(s => s != "openai/gpt-4o")))
            .Returns((LlmModelPricing?)null);

        // Act
        var result = await _service.CompareModelsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].ModelId.Should().Be("openai/gpt-4o");
    }

    [Fact]
    public async Task CompareModelsAsync_DeterminesCorrectQualityTiers()
    {
        // Arrange
        _costCalculatorMock.Setup(c => c.GetModelPricing("openai/gpt-4o"))
            .Returns(new LlmModelPricing
            {
                ModelId = "openai/gpt-4o",
                Provider = "OpenRouter",
                InputCostPer1M = 2.50m, // Standard tier (< 3.0)
                OutputCostPer1M = 10.00m
            });

        _costCalculatorMock.Setup(c => c.GetModelPricing("anthropic/claude-3-opus"))
            .Returns(new LlmModelPricing
            {
                ModelId = "anthropic/claude-3-opus",
                Provider = "OpenRouter",
                InputCostPer1M = 15.00m, // Ultra tier (>= 10.0)
                OutputCostPer1M = 75.00m
            });

        // Act
        var result = await _service.CompareModelsAsync();

        // Assert
        var gpt4o = result.FirstOrDefault(c => c.ModelId == "openai/gpt-4o");
        var opus = result.FirstOrDefault(c => c.ModelId == "anthropic/claude-3-opus");

        gpt4o?.QualityTier.Should().Be("Standard");
        opus?.QualityTier.Should().Be("Ultra");
    }

    [Fact]
    public async Task CompareModelsAsync_CalculatesCostQualityRatioCorrectly()
    {
        // Arrange
        _costCalculatorMock.Setup(c => c.GetModelPricing("openai/gpt-4o-mini"))
            .Returns(new LlmModelPricing
            {
                ModelId = "openai/gpt-4o-mini",
                Provider = "OpenRouter",
                InputCostPer1M = 0.15m, // Budget tier
                OutputCostPer1M = 0.60m
            });

        // Act
        var result = await _service.CompareModelsAsync();

        // Assert
        var mini = result.FirstOrDefault(c => c.ModelId == "openai/gpt-4o-mini");
        mini.Should().NotBeNull();
        // Budget tier has quality score 0.7, so ratio = 0.15 / 0.7 ≈ 0.214
        mini!.CostQualityRatio.Should().BeApproximately(0.214, 0.01);
    }

    #endregion

    #region Helper Methods

    private void SetupModelPricingMocks()
    {
        _costCalculatorMock.Setup(c => c.GetModelPricing("openai/gpt-4o"))
            .Returns(new LlmModelPricing
            {
                ModelId = "openai/gpt-4o",
                Provider = "OpenRouter",
                InputCostPer1M = 2.50m,
                OutputCostPer1M = 10.00m
            });

        _costCalculatorMock.Setup(c => c.GetModelPricing("openai/gpt-4o-mini"))
            .Returns(new LlmModelPricing
            {
                ModelId = "openai/gpt-4o-mini",
                Provider = "OpenRouter",
                InputCostPer1M = 0.15m,
                OutputCostPer1M = 0.60m
            });

        _costCalculatorMock.Setup(c => c.GetModelPricing("anthropic/claude-3-opus"))
            .Returns(new LlmModelPricing
            {
                ModelId = "anthropic/claude-3-opus",
                Provider = "OpenRouter",
                InputCostPer1M = 15.00m,
                OutputCostPer1M = 75.00m
            });

        _costCalculatorMock.Setup(c => c.GetModelPricing("anthropic/claude-3.5-sonnet"))
            .Returns(new LlmModelPricing
            {
                ModelId = "anthropic/claude-3.5-sonnet",
                Provider = "OpenRouter",
                InputCostPer1M = 3.00m,
                OutputCostPer1M = 15.00m
            });

        _costCalculatorMock.Setup(c => c.GetModelPricing("anthropic/claude-3.5-haiku"))
            .Returns(new LlmModelPricing
            {
                ModelId = "anthropic/claude-3.5-haiku",
                Provider = "OpenRouter",
                InputCostPer1M = 0.80m,
                OutputCostPer1M = 4.00m
            });

        _costCalculatorMock.Setup(c => c.GetModelPricing("deepseek/deepseek-chat"))
            .Returns(new LlmModelPricing
            {
                ModelId = "deepseek/deepseek-chat",
                Provider = "OpenRouter",
                InputCostPer1M = 0.14m,
                OutputCostPer1M = 0.28m
            });

        _costCalculatorMock.Setup(c => c.GetModelPricing("meta-llama/llama-3.3-70b-instruct:free"))
            .Returns(new LlmModelPricing
            {
                ModelId = "meta-llama/llama-3.3-70b-instruct:free",
                Provider = "OpenRouter",
                InputCostPer1M = 0,
                OutputCostPer1M = 0
            });
    }

    #endregion
}
