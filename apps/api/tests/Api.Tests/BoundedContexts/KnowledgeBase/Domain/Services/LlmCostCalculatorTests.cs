using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for LlmCostCalculator
/// ISSUE-960: BGAI-018 - Financial cost tracking implementation
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LlmCostCalculatorTests
{
    private readonly ILlmCostCalculator _calculator;

    public LlmCostCalculatorTests()
    {
        var logger = Mock.Of<ILogger<LlmCostCalculator>>();
        _calculator = new LlmCostCalculator(logger);
    }

    [Fact]
    public void CalculateCost_DeepSeek_ReturnsCorrectCost()
    {
        // Arrange - DeepSeek: $0.27/$1.10 per 1M tokens
        var modelId = "deepseek/deepseek-chat";
        var promptTokens = 1000;      // 1K tokens
        var completionTokens = 500;   // 0.5K tokens

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", promptTokens, completionTokens);

        // Assert
        Assert.Equal(modelId, result.ModelId);
        Assert.Equal("OpenRouter", result.Provider);
        Assert.Equal(promptTokens, result.PromptTokens);
        Assert.Equal(completionTokens, result.CompletionTokens);

        // Cost calculation: (1000/1M * $0.27) + (500/1M * $1.10)
        //                 = $0.00027 + $0.00055 = $0.00082
        Assert.Equal(0.00027m, result.InputCost);
        Assert.Equal(0.00055m, result.OutputCost);
        Assert.Equal(0.00082m, result.TotalCost);
        Assert.False(result.IsFree);
    }

    [Fact]
    public void CalculateCost_GeminiPro_ReturnsCorrectCost()
    {
        // Arrange - Gemini Pro: $0.125/$0.375 per 1M tokens
        var modelId = "google/gemini-pro";
        var promptTokens = 10000;      // 10K tokens
        var completionTokens = 2000;   // 2K tokens

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", promptTokens, completionTokens);

        // Assert
        // Cost calculation: (10000/1M * $0.125) + (2000/1M * $0.375)
        //                 = $0.00125 + $0.00075 = $0.002
        Assert.Equal(0.00125m, result.InputCost);
        Assert.Equal(0.00075m, result.OutputCost);
        Assert.Equal(0.002m, result.TotalCost);
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
        var modelId = "deepseek/deepseek-chat";

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
        var modelId = "deepseek/deepseek-chat";

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
        var modelId = "deepseek/deepseek-chat";
        var promptTokens = 1_000_000;
        var completionTokens = 500_000;

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", promptTokens, completionTokens);

        // Assert
        // Cost: (1M/1M * $0.27) + (0.5M/1M * $1.10) = $0.27 + $0.55 = $0.82
        Assert.Equal(0.27m, result.InputCost);
        Assert.Equal(0.55m, result.OutputCost);
        Assert.Equal(0.82m, result.TotalCost);
    }

    [Fact]
    public void GetModelPricing_ExistingModel_ReturnsCorrectPricing()
    {
        // Act
        var pricing = _calculator.GetModelPricing("deepseek/deepseek-chat");

        // Assert
        Assert.NotNull(pricing);
        Assert.Equal("deepseek/deepseek-chat", pricing.ModelId);
        Assert.Equal("OpenRouter", pricing.Provider);
        Assert.Equal(0.27m, pricing.InputCostPer1M);
        Assert.Equal(1.10m, pricing.OutputCostPer1M);
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
        var modelId = "deepseek/deepseek-chat";
        var promptTokens = 123;      // Small number to test rounding
        var completionTokens = 456;

        // Act
        var result = _calculator.CalculateCost(modelId, "OpenRouter", promptTokens, completionTokens);

        // Assert - Should round to 6 decimal places (micro-dollars)
        // Input: 123/1M * $0.27 = $0.00003321 → $0.000033
        // Output: 456/1M * $1.10 = $0.0005016 → $0.000502
        Assert.Equal(0.000033m, result.InputCost);
        Assert.Equal(0.000502m, result.OutputCost);
        Assert.Equal(0.000535m, result.TotalCost);
    }

    [Fact]
    public void CalculateCost_AllSupportedModels_HavePricing()
    {
        // Arrange - Test all configured models (cost-optimized set, Issue #2593 revert)
        var models = new[]
        {
            ("meta-llama/llama-3.3-70b-instruct:free", "OpenRouter"),
            ("meta-llama/llama-3.1-70b-instruct:free", "OpenRouter"),
            ("meta-llama/llama-3.3-70b-instruct", "OpenRouter"),
            ("google/gemini-pro", "OpenRouter"),
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

