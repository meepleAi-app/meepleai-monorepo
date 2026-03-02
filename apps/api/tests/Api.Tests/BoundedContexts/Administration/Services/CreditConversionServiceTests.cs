using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Services;

/// <summary>
/// Unit tests for CreditConversionService (Budget Display System)
/// </summary>
public sealed class CreditConversionServiceTests
{
    private readonly ICreditConversionService _sut;
    private readonly FakeLlmCostCalculator _costCalculator;

    public CreditConversionServiceTests()
    {
        _costCalculator = new FakeLlmCostCalculator();
        _sut = new CreditConversionService(_costCalculator, NullLogger<CreditConversionService>.Instance);
    }

    [Theory]
    [InlineData(0.00001, 1)]       // 1 credit = $0.00001
    [InlineData(0.0001, 10)]       // 10 credits = $0.0001
    [InlineData(0.001, 100)]       // 100 credits = $0.001
    [InlineData(0.01, 1000)]       // 1,000 credits = $0.01
    [InlineData(1.00, 100000)]     // 100,000 credits = $1.00
    public void UsdToCredits_CorrectConversion(decimal usd, decimal expectedCredits)
    {
        // Act
        var actualCredits = _sut.UsdToCredits(usd);

        // Assert
        Assert.Equal(expectedCredits, actualCredits);
    }

    [Fact]
    public void UsdToCredits_RoundsUpForUserProtection()
    {
        // Arrange: $0.000015 should round up to 2 credits (not 1.5)
        var usd = 0.000015m;

        // Act
        var credits = _sut.UsdToCredits(usd);

        // Assert
        Assert.Equal(2m, credits); // Rounds up from 1.5
    }

    [Fact]
    public void UsdToCredits_NegativeThrows()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _sut.UsdToCredits(-0.01m));
    }

    [Theory]
    [InlineData(1, 0.00001)]       // 1 credit = $0.00001
    [InlineData(100, 0.001)]       // 100 credits = $0.001
    [InlineData(1000, 0.01)]       // 1,000 credits = $0.01
    [InlineData(100000, 1.00)]     // 100,000 credits = $1.00
    public void CreditsToUsd_CorrectConversion(decimal credits, decimal expectedUsd)
    {
        // Act
        var actualUsd = _sut.CreditsToUsd(credits);

        // Assert
        Assert.Equal(expectedUsd, actualUsd);
    }

    [Fact]
    public void CreditsToUsd_NegativeThrows()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _sut.CreditsToUsd(-10m));
    }

    [Fact]
    public void EstimateQueryCredits_FreeModel_ReturnsZero()
    {
        // Arrange
        _costCalculator.SetModelPricing("meta-llama/llama-3.3-70b-instruct:free", 0m, 0m);

        // Act
        var credits = _sut.EstimateQueryCredits(1000, "meta-llama/llama-3.3-70b-instruct:free");

        // Assert
        Assert.Equal(0m, credits);
    }

    [Fact]
    public void EstimateQueryCredits_PaidModel_EstimatesCorrectly()
    {
        // Arrange: DeepSeek pricing
        _costCalculator.SetModelPricing("deepseek/deepseek-chat", inputCostPer1M: 0.27m, outputCostPer1M: 1.10m);

        // Act: Estimate for 10,000 tokens
        var credits = _sut.EstimateQueryCredits(10_000, "deepseek/deepseek-chat");

        // Expected calculation:
        // Input tokens: 10,000 * 0.7 = 7,000
        // Output tokens: 10,000 * 0.3 = 3,000
        // Input cost: 7,000 / 1,000,000 * 0.27 = $0.00189
        // Output cost: 3,000 / 1,000,000 * 1.10 = $0.0033
        // Total: $0.00519
        // Credits: $0.00519 * 100,000 = 519 credits (rounded up)

        // Assert
        Assert.InRange(credits, 500m, 550m); // Allow for rounding
    }

    [Fact]
    public void EstimateQueryCredits_UnknownModel_ReturnsZero()
    {
        // Arrange: Unknown model (not in pricing database)

        // Act
        var credits = _sut.EstimateQueryCredits(1000, "unknown/model");

        // Assert
        Assert.Equal(0m, credits); // Treats as free
    }

    [Fact]
    public void EstimateQueryCredits_NegativeTokensThrows()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _sut.EstimateQueryCredits(-100, "deepseek/deepseek-chat"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EstimateQueryCredits_EmptyModelIdThrows(string? modelId)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _sut.EstimateQueryCredits(1000, modelId!));
    }

    /// <summary>
    /// Fake cost calculator for testing
    /// </summary>
    private sealed class FakeLlmCostCalculator : ILlmCostCalculator
    {
        private readonly Dictionary<string, LlmModelPricing> _pricing = new(StringComparer.Ordinal);

        public void SetModelPricing(string modelId, decimal inputCostPer1M, decimal outputCostPer1M)
        {
            _pricing[modelId] = new LlmModelPricing
            {
                ModelId = modelId,
                Provider = "Test",
                InputCostPer1M = inputCostPer1M,
                OutputCostPer1M = outputCostPer1M
            };
        }

        public LlmCostCalculation CalculateCost(string modelId, string provider, int promptTokens, int completionTokens)
        {
            throw new NotSupportedException();
        }

        public LlmModelPricing? GetModelPricing(string modelId)
        {
            return _pricing.TryGetValue(modelId, out var pricing) ? pricing : null;
        }
    }
}
