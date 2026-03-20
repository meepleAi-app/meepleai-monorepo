using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Unit tests for TokenUsage value object.
/// Issue #1694: Verify token usage tracking and cost calculation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        tokenUsage.PromptTokens.Should().Be(100);
        tokenUsage.CompletionTokens.Should().Be(50);
        tokenUsage.TotalTokens.Should().Be(150);
        tokenUsage.EstimatedCost.Should().Be(0.002m);
        tokenUsage.ModelId.Should().Be("openai/gpt-4o-mini");
        tokenUsage.Provider.Should().Be("OpenRouter");
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

        exception.Message.Should().Contain("Prompt tokens cannot be negative");
        exception.ParamName.Should().Be("promptTokens");
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

        exception.Message.Should().Contain("Completion tokens cannot be negative");
        exception.ParamName.Should().Be("completionTokens");
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

        exception.Message.Should().Contain("Total tokens cannot be negative");
        exception.ParamName.Should().Be("totalTokens");
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

        exception.Message.Should().Contain("Estimated cost cannot be negative");
        exception.ParamName.Should().Be("estimatedCost");
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

        exception.Message.Should().Contain("Model ID cannot be empty");
        exception.ParamName.Should().Be("modelId");
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

        exception.Message.Should().Contain("Provider cannot be empty");
        exception.ParamName.Should().Be("provider");
    }

    [Fact]
    public void Empty_ReturnsZeroValues()
    {
        // Act
        var empty = TokenUsage.Empty;

        // Assert
        empty.PromptTokens.Should().Be(0);
        empty.CompletionTokens.Should().Be(0);
        empty.TotalTokens.Should().Be(0);
        empty.EstimatedCost.Should().Be(0m);
        empty.ModelId.Should().Be("none");
        empty.Provider.Should().Be("none");
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
        tokenUsage.PromptTokens.Should().Be(100);
        tokenUsage.CompletionTokens.Should().Be(50);
        tokenUsage.TotalTokens.Should().Be(150);
        tokenUsage.EstimatedCost.Should().Be(0.002m);
        tokenUsage.ModelId.Should().Be("openai/gpt-4o-mini");
        tokenUsage.Provider.Should().Be("OpenRouter");
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var tokenUsage1 = new TokenUsage(100, 50, 150, 0.002m, "openai/gpt-4o-mini", "OpenRouter");
        var tokenUsage2 = new TokenUsage(100, 50, 150, 0.002m, "openai/gpt-4o-mini", "OpenRouter");

        // Assert
        tokenUsage2.Should().Be(tokenUsage1);
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
        tokenUsage2.Should().NotBe(tokenUsage1);
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
        result.Should().Contain("Total=150");
        result.Should().Contain("Prompt=100");
        result.Should().Contain("Completion=50");
        result.Should().Contain("Cost=");
        result.Should().Contain("Model=openai/gpt-4o-mini");
        result.Should().Contain("Provider=OpenRouter");
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
        tokenUsage.ModelId.Should().Be("openai/gpt-4o-mini");
        tokenUsage.Provider.Should().Be("OpenRouter");
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
        tokenUsage.PromptTokens.Should().Be(promptTokens);
        tokenUsage.CompletionTokens.Should().Be(completionTokens);
        tokenUsage.TotalTokens.Should().Be(totalTokens);
        tokenUsage.EstimatedCost.Should().Be((decimal)cost);
    }
}

