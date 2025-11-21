using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for LlmCostCalculator
/// ISSUE-960: BGAI-018 - Financial cost tracking implementation
/// </summary>
public class LlmCostCalculatorTests
{
    private readonly ILlmCostCalculator _calculator;

    public LlmCostCalculatorTests()
    {
        var logger = Mock.Of<ILogger<LlmCostCalculator>>();
        _calculator = new LlmCostCalculator(logger);
    }

    [Fact]
    public void CalculateCost_GptMini_ReturnsCorrectCost()
    {
        // Arrange - GPT-4o-mini: $0.15/$0.60 per 1M tokens
        var modelId = "openai/gpt-4o-mini";
        var promptTokens = 1000;      // 1K tokens
        var completionTokens = 500;   // 0.5K tokens

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", promptTokens, completionTokens);

        // Assert
        Assert.Equal(modelId, result.ModelId);
        Assert.Equal("OpenRouter", result.Provider);
        Assert.Equal(promptTokens, result.PromptTokens);
        Assert.Equal(completionTokens, result.CompletionTokens);

        // Cost calculation: (1000/1M * $0.15) + (500/1M * $0.60)
        //                 = $0.00015 + $0.0003 = $0.00045
        Assert.Equal(0.00015m, result.InputCost);
        Assert.Equal(0.0003m, result.OutputCost);
        Assert.Equal(0.00045m, result.TotalCost);
        Assert.False(result.IsFree);
    }

    [Fact]
    public void CalculateCost_ClaudeHaiku_ReturnsCorrectCost()
    {
        // Arrange - Claude 3.5 Haiku: $0.80/$4.00 per 1M tokens
        var modelId = "anthropic/claude-3.5-haiku";
        var promptTokens = 10000;      // 10K tokens
        var completionTokens = 2000;   // 2K tokens

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", promptTokens, completionTokens);

        // Assert
        // Cost calculation: (10000/1M * $0.80) + (2000/1M * $4.00)
        //                 = $0.008 + $0.008 = $0.016
        Assert.Equal(0.008m, result.InputCost);
        Assert.Equal(0.008m, result.OutputCost);
        Assert.Equal(0.016m, result.TotalCost);
        Assert.False(result.IsFree);
    }

    [Fact]
    public void CalculateCost_FreeTierModel_ReturnsZeroCost()
    {
        // Arrange - Llama 3.3 70B free tier
        var modelId = "meta-llama/llama-3.3-70b-instruct:free";
        var promptTokens = 5000;
        var completionTokens = 3000;

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", promptTokens, completionTokens);

        // Assert
        Assert.Equal(0m, result.InputCost);
        Assert.Equal(0m, result.OutputCost);
        Assert.Equal(0m, result.TotalCost);
        Assert.True(result.IsFree);
    }

    [Fact]
    public void CalculateCost_OllamaModel_ReturnsZeroCost()
    {
        // Arrange - Local Ollama model (self-hosted)
        var modelId = "llama3:8b";
        var promptTokens = 8000;
        var completionTokens = 4000;

        // Act
        var result = _calculator.CalculateCost(modelId, "Ollama", promptTokens, completionTokens);

        // Assert
        Assert.Equal(0m, result.InputCost);
        Assert.Equal(0m, result.OutputCost);
        Assert.Equal(0m, result.TotalCost);
        Assert.True(result.IsFree);
    }

    [Fact]
    public void CalculateCost_UnknownModel_ReturnsFreeAndWarnsUser()
    {
        // Arrange - Model not in pricing database
        var modelId = "unknown/model";
        var promptTokens = 1000;
        var completionTokens = 500;

        // Act
        var result = _calculator.CalculateCost(modelId, "Unknown", promptTokens, completionTokens);

        // Assert - Should treat as free
        Assert.Equal(modelId, result.ModelId);
        Assert.Equal(0m, result.TotalCost);
        Assert.True(result.IsFree);
    }

    [Fact]
    public void CalculateCost_NegativeTokens_ReturnsEmpty()
    {
        // Arrange
        var modelId = "openai/gpt-4o-mini";

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", -100, 500);

        // Assert - Should return empty calculation
        Assert.Equal("unknown", result.ModelId);
        Assert.Equal(0, result.PromptTokens);
        Assert.Equal(0, result.CompletionTokens);
        Assert.Equal(0m, result.TotalCost);
    }

    [Fact]
    public void CalculateCost_ZeroTokens_ReturnsZeroCost()
    {
        // Arrange
        var modelId = "openai/gpt-4o-mini";

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", 0, 0);

        // Assert
        Assert.Equal(0m, result.TotalCost);
        Assert.True(result.IsFree);
    }

    [Fact]
    public void CalculateCost_LargeTokenCount_HandlesCorrectly()
    {
        // Arrange - 1M tokens (exact 1M boundary)
        var modelId = "openai/gpt-4o-mini";
        var promptTokens = 1_000_000;
        var completionTokens = 500_000;

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", promptTokens, completionTokens);

        // Assert
        // Cost: (1M/1M * $0.15) + (0.5M/1M * $0.60) = $0.15 + $0.30 = $0.45
        Assert.Equal(0.15m, result.InputCost);
        Assert.Equal(0.30m, result.OutputCost);
        Assert.Equal(0.45m, result.TotalCost);
    }

    [Fact]
    public void GetModelPricing_ExistingModel_ReturnsCorrectPricing()
    {
        // Act
        var pricing = _calculator.GetModelPricing("openai/gpt-4o-mini");

        // Assert
        Assert.NotNull(pricing);
        Assert.Equal("openai/gpt-4o-mini", pricing.ModelId);
        Assert.Equal("OpenRouter", pricing.Provider);
        Assert.Equal(0.15m, pricing.InputCostPer1M);
        Assert.Equal(0.60m, pricing.OutputCostPer1M);
        Assert.False(pricing.IsFree);
    }

    [Fact]
    public void GetModelPricing_NonExistentModel_ReturnsNull()
    {
        // Act
        var pricing = _calculator.GetModelPricing("nonexistent/model");

        // Assert
        Assert.Null(pricing);
    }

    [Fact]
    public void CalculateCost_RoundingPrecision_SixDecimalPlaces()
    {
        // Arrange - Test micro-dollar precision
        var modelId = "openai/gpt-4o-mini";
        var promptTokens = 123;      // Small number to test rounding
        var completionTokens = 456;

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", promptTokens, completionTokens);

        // Assert - Should round to 6 decimal places (micro-dollars)
        // Input: 123/1M * $0.15 = $0.00001845 → $0.000018
        // Output: 456/1M * $0.60 = $0.0002736 → $0.000274
        Assert.Equal(0.000018m, result.InputCost);
        Assert.Equal(0.000274m, result.OutputCost);
        Assert.Equal(0.000292m, result.TotalCost);
    }

    [Fact]
    public void CalculateCost_AllSupportedModels_HavePricing()
    {
        // Arrange - Test all configured models
        var models = new[]
        {
            ("openai/gpt-4o-mini", "OpenRouter"),
            ("openai/gpt-4o", "OpenRouter"),
            ("anthropic/claude-3.5-haiku", "OpenRouter"),
            ("anthropic/claude-3.5-sonnet", "OpenRouter"),
            ("anthropic/claude-3-opus", "OpenRouter"),
            ("meta-llama/llama-3.3-70b-instruct:free", "OpenRouter"),
            ("meta-llama/llama-3.1-70b-instruct:free", "OpenRouter"),
            ("deepseek/deepseek-chat", "OpenRouter"),
            ("llama3:8b", "Ollama"),
            ("llama3:70b", "Ollama"),
            ("mistral", "Ollama")
        };

        // Act & Assert
        foreach (var (modelId, provider) in models)
        {
            var pricing = _calculator.GetModelPricing(modelId);
            Assert.NotNull(pricing);
            Assert.Equal(modelId, pricing.ModelId);
            Assert.Equal(provider, pricing.Provider);

            // Verify calculation works
            var cost = _calculator.CalculateCost(modelId, provider, 1000, 500);
            Assert.NotNull(cost);
            Assert.Equal(modelId, cost.ModelId);
        }
    }
}
