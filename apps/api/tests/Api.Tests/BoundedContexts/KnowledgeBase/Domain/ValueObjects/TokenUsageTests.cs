using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Unit tests for TokenUsage value object.
/// Issue #1694: Verify token usage tracking and cost calculation.
/// </summary>
public class TokenUsageTests
{
    [Fact]
    public void Constructor_ValidInputs_CreatesInstance()
    {
        // Act
        var tokenUsage = new TokenUsage(
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: 150,
            estimatedCost: 0.002m,
            modelId: "openai/gpt-4o-mini",
            provider: "OpenRouter");

        // Assert
        Assert.Equal(100, tokenUsage.PromptTokens);
        Assert.Equal(50, tokenUsage.CompletionTokens);
        Assert.Equal(150, tokenUsage.TotalTokens);
        Assert.Equal(0.002m, tokenUsage.EstimatedCost);
        Assert.Equal("openai/gpt-4o-mini", tokenUsage.ModelId);
        Assert.Equal("OpenRouter", tokenUsage.Provider);
    }

    [Fact]
    public void Constructor_NegativePromptTokens_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new TokenUsage(
            promptTokens: -1,
            completionTokens: 50,
            totalTokens: 150,
            estimatedCost: 0.002m,
            modelId: "openai/gpt-4o-mini",
            provider: "OpenRouter"));

        Assert.Contains("Prompt tokens cannot be negative", exception.Message);
        Assert.Equal("promptTokens", exception.ParamName);
    }

    [Fact]
    public void Constructor_NegativeCompletionTokens_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new TokenUsage(
            promptTokens: 100,
            completionTokens: -1,
            totalTokens: 150,
            estimatedCost: 0.002m,
            modelId: "openai/gpt-4o-mini",
            provider: "OpenRouter"));

        Assert.Contains("Completion tokens cannot be negative", exception.Message);
        Assert.Equal("completionTokens", exception.ParamName);
    }

    [Fact]
    public void Constructor_NegativeTotalTokens_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new TokenUsage(
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: -1,
            estimatedCost: 0.002m,
            modelId: "openai/gpt-4o-mini",
            provider: "OpenRouter"));

        Assert.Contains("Total tokens cannot be negative", exception.Message);
        Assert.Equal("totalTokens", exception.ParamName);
    }

    [Fact]
    public void Constructor_NegativeCost_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new TokenUsage(
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: 150,
            estimatedCost: -0.001m,
            modelId: "openai/gpt-4o-mini",
            provider: "OpenRouter"));

        Assert.Contains("Estimated cost cannot be negative", exception.Message);
        Assert.Equal("estimatedCost", exception.ParamName);
    }

    [Fact]
    public void Constructor_EmptyModelId_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new TokenUsage(
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: 150,
            estimatedCost: 0.002m,
            modelId: "",
            provider: "OpenRouter"));

        Assert.Contains("Model ID cannot be empty", exception.Message);
        Assert.Equal("modelId", exception.ParamName);
    }

    [Fact]
    public void Constructor_EmptyProvider_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new TokenUsage(
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: 150,
            estimatedCost: 0.002m,
            modelId: "openai/gpt-4o-mini",
            provider: ""));

        Assert.Contains("Provider cannot be empty", exception.Message);
        Assert.Equal("provider", exception.ParamName);
    }

    [Fact]
    public void Empty_ReturnsZeroValues()
    {
        // Act
        var empty = TokenUsage.Empty;

        // Assert
        Assert.Equal(0, empty.PromptTokens);
        Assert.Equal(0, empty.CompletionTokens);
        Assert.Equal(0, empty.TotalTokens);
        Assert.Equal(0m, empty.EstimatedCost);
        Assert.Equal("none", empty.ModelId);
        Assert.Equal("none", empty.Provider);
    }

    [Fact]
    public void FromLlmResult_ValidInputs_CreatesTokenUsage()
    {
        // Arrange
        var llmUsage = new LlmUsage(
            PromptTokens: 100,
            CompletionTokens: 50,
            TotalTokens: 150);

        var llmCost = new LlmCost
        {
            InputCost = 0.001m,
            OutputCost = 0.001m,
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter"
        };

        // Act
        var tokenUsage = TokenUsage.FromLlmResult(llmUsage, llmCost);

        // Assert
        Assert.Equal(100, tokenUsage.PromptTokens);
        Assert.Equal(50, tokenUsage.CompletionTokens);
        Assert.Equal(150, tokenUsage.TotalTokens);
        Assert.Equal(0.002m, tokenUsage.EstimatedCost);
        Assert.Equal("openai/gpt-4o-mini", tokenUsage.ModelId);
        Assert.Equal("OpenRouter", tokenUsage.Provider);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var tokenUsage1 = new TokenUsage(100, 50, 150, 0.002m, "openai/gpt-4o-mini", "OpenRouter");
        var tokenUsage2 = new TokenUsage(100, 50, 150, 0.002m, "openai/gpt-4o-mini", "OpenRouter");

        // Assert
        Assert.Equal(tokenUsage1, tokenUsage2);
        Assert.True(tokenUsage1 == tokenUsage2);
        Assert.False(tokenUsage1 != tokenUsage2);
    }

    [Fact]
    public void Equality_DifferentTokens_AreNotEqual()
    {
        // Arrange
        var tokenUsage1 = new TokenUsage(100, 50, 150, 0.002m, "openai/gpt-4o-mini", "OpenRouter");
        var tokenUsage2 = new TokenUsage(200, 100, 300, 0.004m, "openai/gpt-4o-mini", "OpenRouter");

        // Assert
        Assert.NotEqual(tokenUsage1, tokenUsage2);
        Assert.False(tokenUsage1 == tokenUsage2);
        Assert.True(tokenUsage1 != tokenUsage2);
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        // Arrange
        var tokenUsage = new TokenUsage(100, 50, 150, 0.002m, "openai/gpt-4o-mini", "OpenRouter");

        // Act
        var result = tokenUsage.ToString();

        // Assert - Verify key components are present in output
        Assert.Contains("Total=150", result);
        Assert.Contains("Prompt=100", result);
        Assert.Contains("Completion=50", result);
        Assert.Contains("Cost=", result);
        Assert.Contains("Model=openai/gpt-4o-mini", result);
        Assert.Contains("Provider=OpenRouter", result);
        // Verify it's in TokenUsage format
        Assert.StartsWith("TokenUsage(", result);
    }

    [Fact]
    public void Constructor_TrimsWhitespace_InModelIdAndProvider()
    {
        // Act
        var tokenUsage = new TokenUsage(
            promptTokens: 100,
            completionTokens: 50,
            totalTokens: 150,
            estimatedCost: 0.002m,
            modelId: "  openai/gpt-4o-mini  ",
            provider: "  OpenRouter  ");

        // Assert
        Assert.Equal("openai/gpt-4o-mini", tokenUsage.ModelId);
        Assert.Equal("OpenRouter", tokenUsage.Provider);
    }

    [Theory]
    [InlineData(0, 0, 0, 0.0)]
    [InlineData(100, 50, 150, 0.002)]
    [InlineData(1000, 500, 1500, 0.02)]
    public void Constructor_ValidRanges_CreatesInstance(
        int promptTokens,
        int completionTokens,
        int totalTokens,
        double cost)
    {
        // Act
        var tokenUsage = new TokenUsage(
            promptTokens,
            completionTokens,
            totalTokens,
            (decimal)cost,
            "openai/gpt-4o-mini",
            "OpenRouter");

        // Assert
        Assert.Equal(promptTokens, tokenUsage.PromptTokens);
        Assert.Equal(completionTokens, tokenUsage.CompletionTokens);
        Assert.Equal(totalTokens, tokenUsage.TotalTokens);
        Assert.Equal((decimal)cost, tokenUsage.EstimatedCost);
    }
}

