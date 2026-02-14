using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Services;

/// <summary>
/// Unit tests for LlmCostCalculator service (Issue #3727)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class LlmCostCalculatorTests
{
    private readonly LlmCostCalculator _calculator;

    public LlmCostCalculatorTests()
    {
        var loggerMock = new Mock<ILogger<LlmCostCalculator>>();
        _calculator = new LlmCostCalculator(loggerMock.Object);
    }

    // ========== Known Model Pricing Tests ==========

    [Fact]
    public void CalculateCost_FreeModel_ShouldReturnZeroCost()
    {
        var result = _calculator.CalculateCost(
            "meta-llama/llama-3.3-70b-instruct:free", "OpenRouter", 1000, 500);

        result.TotalCost.Should().Be(0m);
        result.InputCost.Should().Be(0m);
        result.OutputCost.Should().Be(0m);
        result.IsFree.Should().BeTrue();
    }

    [Fact]
    public void CalculateCost_OllamaModel_ShouldReturnZeroCost()
    {
        var result = _calculator.CalculateCost(
            "llama3:8b", "Ollama", 5000, 3000);

        result.TotalCost.Should().Be(0m);
        result.IsFree.Should().BeTrue();
    }

    [Fact]
    public void CalculateCost_DeepSeekChat_ShouldCalculateCorrectly()
    {
        // DeepSeek: input=$0.27/1M, output=$1.10/1M
        var result = _calculator.CalculateCost(
            "deepseek/deepseek-chat", "OpenRouter", 1_000_000, 1_000_000);

        result.InputCost.Should().Be(0.27m);
        result.OutputCost.Should().Be(1.10m);
        result.TotalCost.Should().Be(1.37m);
    }

    [Fact]
    public void CalculateCost_PaidLlama_ShouldCalculateCorrectly()
    {
        // Paid Llama: input=$0.59/1M, output=$0.79/1M
        var result = _calculator.CalculateCost(
            "meta-llama/llama-3.3-70b-instruct", "OpenRouter", 1_000_000, 1_000_000);

        result.InputCost.Should().Be(0.59m);
        result.OutputCost.Should().Be(0.79m);
        result.TotalCost.Should().Be(1.38m);
    }

    [Fact]
    public void CalculateCost_GeminiPro_ShouldCalculateCorrectly()
    {
        // Gemini Pro: input=$0.125/1M, output=$0.375/1M
        var result = _calculator.CalculateCost(
            "google/gemini-pro", "OpenRouter", 1_000_000, 1_000_000);

        result.InputCost.Should().Be(0.125m);
        result.OutputCost.Should().Be(0.375m);
        result.TotalCost.Should().Be(0.50m);
    }

    // ========== Unknown Model Tests ==========

    [Fact]
    public void CalculateCost_UnknownModel_ShouldReturnZeroCost()
    {
        var result = _calculator.CalculateCost(
            "unknown/model-xyz", "SomeProvider", 1000, 500);

        result.TotalCost.Should().Be(0m);
        result.ModelId.Should().Be("unknown/model-xyz");
        result.Provider.Should().Be("SomeProvider");
    }

    // ========== Token Count Validation ==========

    [Fact]
    public void CalculateCost_WithNegativePromptTokens_ShouldReturnEmpty()
    {
        var result = _calculator.CalculateCost(
            "deepseek/deepseek-chat", "OpenRouter", -100, 500);

        result.Should().Be(LlmCostCalculation.Empty);
    }

    [Fact]
    public void CalculateCost_WithNegativeCompletionTokens_ShouldReturnEmpty()
    {
        var result = _calculator.CalculateCost(
            "deepseek/deepseek-chat", "OpenRouter", 1000, -500);

        result.Should().Be(LlmCostCalculation.Empty);
    }

    [Fact]
    public void CalculateCost_WithZeroTokens_ShouldReturnZeroCost()
    {
        var result = _calculator.CalculateCost(
            "deepseek/deepseek-chat", "OpenRouter", 0, 0);

        result.TotalCost.Should().Be(0m);
        result.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void CalculateCost_ShouldTrackTokenCounts()
    {
        var result = _calculator.CalculateCost(
            "deepseek/deepseek-chat", "OpenRouter", 3000, 2000);

        result.PromptTokens.Should().Be(3000);
        result.CompletionTokens.Should().Be(2000);
        result.TotalTokens.Should().Be(5000);
    }

    // ========== Cost Precision Tests ==========

    [Fact]
    public void CalculateCost_ShouldRoundTo6DecimalPlaces()
    {
        // Small token counts should produce micro-dollar precision
        var result = _calculator.CalculateCost(
            "deepseek/deepseek-chat", "OpenRouter", 100, 50);

        // 100 / 1M * 0.27 = 0.000027
        result.InputCost.Should().Be(0.000027m);
        // 50 / 1M * 1.10 = 0.000055
        result.OutputCost.Should().Be(0.000055m);
    }

    [Fact]
    public void CalculateCost_LargeTokenCounts_ShouldHandleCorrectly()
    {
        // 10M tokens per side
        var result = _calculator.CalculateCost(
            "deepseek/deepseek-chat", "OpenRouter", 10_000_000, 10_000_000);

        result.InputCost.Should().Be(2.70m);
        result.OutputCost.Should().Be(11.00m);
        result.TotalCost.Should().Be(13.70m);
    }

    // ========== GetModelPricing Tests ==========

    [Fact]
    public void GetModelPricing_KnownModel_ShouldReturnPricing()
    {
        var pricing = _calculator.GetModelPricing("deepseek/deepseek-chat");

        pricing.Should().NotBeNull();
        pricing!.ModelId.Should().Be("deepseek/deepseek-chat");
        pricing.Provider.Should().Be("OpenRouter");
        pricing.InputCostPer1M.Should().Be(0.27m);
        pricing.OutputCostPer1M.Should().Be(1.10m);
        pricing.IsFree.Should().BeFalse();
    }

    [Fact]
    public void GetModelPricing_FreeModel_ShouldReturnFreeFlag()
    {
        var pricing = _calculator.GetModelPricing("meta-llama/llama-3.3-70b-instruct:free");

        pricing.Should().NotBeNull();
        pricing!.IsFree.Should().BeTrue();
    }

    [Fact]
    public void GetModelPricing_UnknownModel_ShouldReturnNull()
    {
        var pricing = _calculator.GetModelPricing("nonexistent/model");

        pricing.Should().BeNull();
    }

    [Fact]
    public void GetModelPricing_AllOllamaModels_ShouldBeFree()
    {
        var ollamaModels = new[] { "llama3:8b", "llama3:70b", "mistral" };

        foreach (var modelId in ollamaModels)
        {
            var pricing = _calculator.GetModelPricing(modelId);
            pricing.Should().NotBeNull($"Model {modelId} should have pricing");
            pricing!.IsFree.Should().BeTrue($"Ollama model {modelId} should be free");
        }
    }

    // ========== LlmModelPricing Static Tests ==========

    [Fact]
    public void LlmModelPricing_Free_ShouldHaveZeroCosts()
    {
        LlmModelPricing.Free.InputCostPer1M.Should().Be(0m);
        LlmModelPricing.Free.OutputCostPer1M.Should().Be(0m);
        LlmModelPricing.Free.IsFree.Should().BeTrue();
    }

    // ========== LlmCostCalculation Static Tests ==========

    [Fact]
    public void LlmCostCalculation_Empty_ShouldHaveZeroValues()
    {
        LlmCostCalculation.Empty.TotalCost.Should().Be(0m);
        LlmCostCalculation.Empty.TotalTokens.Should().Be(0);
        LlmCostCalculation.Empty.IsFree.Should().BeTrue();
    }
}
