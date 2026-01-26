using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Models;

/// <summary>
/// Tests for the LlmCostCalculation record.
/// Issue #3025: Backend 90% Coverage Target - Phase 11
/// </summary>
[Trait("Category", "Unit")]
public sealed class LlmCostCalculationTests
{
    #region Creation Tests

    [Fact]
    public void Create_WithValidData_ReturnsLlmCostCalculation()
    {
        // Act
        var calculation = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0006m
        };

        // Assert
        calculation.ModelId.Should().Be("openai/gpt-4o-mini");
        calculation.Provider.Should().Be("OpenRouter");
        calculation.PromptTokens.Should().Be(1000);
        calculation.CompletionTokens.Should().Be(500);
        calculation.InputCost.Should().Be(0.00015m);
        calculation.OutputCost.Should().Be(0.0006m);
    }

    #endregion

    #region Computed Property Tests

    [Fact]
    public void TotalTokens_ReturnsPromptPlusCompletion()
    {
        // Arrange
        var calculation = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0006m
        };

        // Act & Assert
        calculation.TotalTokens.Should().Be(1500);
    }

    [Fact]
    public void TotalCost_ReturnsInputPlusOutput()
    {
        // Arrange
        var calculation = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0006m
        };

        // Act & Assert
        calculation.TotalCost.Should().Be(0.00075m);
    }

    [Fact]
    public void IsFree_WithZeroCost_ReturnsTrue()
    {
        // Arrange
        var calculation = new LlmCostCalculation
        {
            ModelId = "meta-llama/llama-3-8b:free",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0m,
            OutputCost = 0m
        };

        // Act & Assert
        calculation.IsFree.Should().BeTrue();
    }

    [Fact]
    public void IsFree_WithNonZeroCost_ReturnsFalse()
    {
        // Arrange
        var calculation = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0006m
        };

        // Act & Assert
        calculation.IsFree.Should().BeFalse();
    }

    [Fact]
    public void IsFree_WithOnlyInputCost_ReturnsFalse()
    {
        // Arrange
        var calculation = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 0,
            InputCost = 0.00015m,
            OutputCost = 0m
        };

        // Act & Assert
        calculation.IsFree.Should().BeFalse();
    }

    [Fact]
    public void IsFree_WithOnlyOutputCost_ReturnsFalse()
    {
        // Arrange
        var calculation = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 0,
            CompletionTokens = 500,
            InputCost = 0m,
            OutputCost = 0.0006m
        };

        // Act & Assert
        calculation.IsFree.Should().BeFalse();
    }

    #endregion

    #region Empty Static Property Tests

    [Fact]
    public void Empty_HasExpectedValues()
    {
        // Act
        var empty = LlmCostCalculation.Empty;

        // Assert
        empty.ModelId.Should().Be("unknown");
        empty.Provider.Should().Be("Unknown");
        empty.PromptTokens.Should().Be(0);
        empty.CompletionTokens.Should().Be(0);
        empty.InputCost.Should().Be(0);
        empty.OutputCost.Should().Be(0);
    }

    [Fact]
    public void Empty_TotalTokensIsZero()
    {
        // Act & Assert
        LlmCostCalculation.Empty.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void Empty_TotalCostIsZero()
    {
        // Act & Assert
        LlmCostCalculation.Empty.TotalCost.Should().Be(0);
    }

    [Fact]
    public void Empty_IsFreeReturnsTrue()
    {
        // Act & Assert
        LlmCostCalculation.Empty.IsFree.Should().BeTrue();
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void TwoCalculations_WithSameValues_AreEqual()
    {
        // Arrange
        var calc1 = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0006m
        };

        var calc2 = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0006m
        };

        // Assert
        calc1.Should().Be(calc2);
        (calc1 == calc2).Should().BeTrue();
    }

    [Fact]
    public void TwoCalculations_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var calc1 = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0006m
        };

        var calc2 = new LlmCostCalculation
        {
            ModelId = "anthropic/claude-3.5-haiku",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0006m
        };

        // Assert
        calc1.Should().NotBe(calc2);
        (calc1 != calc2).Should().BeTrue();
    }

    #endregion

    #region With Expression Tests

    [Fact]
    public void WithExpression_CreatesNewRecordWithModifiedValues()
    {
        // Arrange
        var original = new LlmCostCalculation
        {
            ModelId = "openai/gpt-4o-mini",
            Provider = "OpenRouter",
            PromptTokens = 1000,
            CompletionTokens = 500,
            InputCost = 0.00015m,
            OutputCost = 0.0006m
        };

        // Act
        var modified = original with { CompletionTokens = 750, OutputCost = 0.0009m };

        // Assert
        modified.ModelId.Should().Be("openai/gpt-4o-mini");
        modified.CompletionTokens.Should().Be(750);
        modified.OutputCost.Should().Be(0.0009m);
        modified.TotalTokens.Should().Be(1750);

        // Original unchanged
        original.CompletionTokens.Should().Be(500);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TotalTokens_WithZeroTokens_ReturnsZero()
    {
        // Arrange
        var calculation = new LlmCostCalculation
        {
            ModelId = "test",
            Provider = "Test",
            PromptTokens = 0,
            CompletionTokens = 0,
            InputCost = 0m,
            OutputCost = 0m
        };

        // Act & Assert
        calculation.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void TotalTokens_WithLargeValues_CalculatesCorrectly()
    {
        // Arrange
        var calculation = new LlmCostCalculation
        {
            ModelId = "test",
            Provider = "Test",
            PromptTokens = 100000,
            CompletionTokens = 32000,
            InputCost = 15m,
            OutputCost = 96m
        };

        // Act & Assert
        calculation.TotalTokens.Should().Be(132000);
        calculation.TotalCost.Should().Be(111m);
    }

    [Fact]
    public void Cost_WithHighPrecision_MaintainsPrecision()
    {
        // Arrange
        var calculation = new LlmCostCalculation
        {
            ModelId = "test",
            Provider = "Test",
            PromptTokens = 1,
            CompletionTokens = 1,
            InputCost = 0.000001m,
            OutputCost = 0.000002m
        };

        // Act & Assert
        calculation.TotalCost.Should().Be(0.000003m);
    }

    #endregion
}
