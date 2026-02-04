// =============================================================================
// MeepleAI - RAG Plugin System Tests
// Issue #3415 - DAG Orchestrator
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Pipeline;

public class ConditionEvaluatorTests
{
    private readonly ConditionEvaluator _evaluator;

    public ConditionEvaluatorTests()
    {
        _evaluator = new ConditionEvaluator();
    }

    #region Basic Keywords Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("always")]
    [InlineData("ALWAYS")]
    [InlineData("true")]
    public void Evaluate_AlwaysTrueConditions_ReturnsTrue(string? condition)
    {
        // Arrange
        var output = CreateSuccessfulOutput();

        // Act
        var result = _evaluator.Evaluate(condition, output);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("never")]
    [InlineData("NEVER")]
    [InlineData("false")]
    public void Evaluate_AlwaysFalseConditions_ReturnsFalse(string condition)
    {
        // Arrange
        var output = CreateSuccessfulOutput();

        // Act
        var result = _evaluator.Evaluate(condition, output);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Direct Property Tests

    [Fact]
    public void Evaluate_SuccessEqualsTrue_ReturnsTrue()
    {
        // Arrange
        var output = CreateSuccessfulOutput();

        // Act
        var result = _evaluator.Evaluate("success == true", output);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_SuccessEqualsFalse_ReturnsFalse()
    {
        // Arrange
        var output = CreateSuccessfulOutput();

        // Act
        var result = _evaluator.Evaluate("success == false", output);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ConfidenceGreaterThan_ReturnsTrue()
    {
        // Arrange
        var output = CreateOutputWithConfidence(0.85);

        // Act
        var result = _evaluator.Evaluate("confidence >= 0.7", output);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ConfidenceLessThan_ReturnsFalse()
    {
        // Arrange
        var output = CreateOutputWithConfidence(0.5);

        // Act
        var result = _evaluator.Evaluate("confidence >= 0.7", output);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ConfidenceLessThanOrEqual_ReturnsTrue()
    {
        // Arrange
        var output = CreateOutputWithConfidence(0.7);

        // Act
        var result = _evaluator.Evaluate("confidence <= 0.7", output);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region JSON Field Tests

    [Fact]
    public void Evaluate_JsonFieldStringEquals_ReturnsTrue()
    {
        // Arrange
        var output = CreateOutputWithResult("""{"type": "rules"}""");

        // Act
        var result = _evaluator.Evaluate("output.type == 'rules'", output);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_JsonFieldStringNotEquals_ReturnsFalse()
    {
        // Arrange
        var output = CreateOutputWithResult("""{"type": "faq"}""");

        // Act
        var result = _evaluator.Evaluate("output.type == 'rules'", output);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_JsonFieldNumericComparison_ReturnsTrue()
    {
        // Arrange
        var output = CreateOutputWithResult("""{"score": 0.95}""");

        // Act
        var result = _evaluator.Evaluate("result.score > 0.9", output);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_JsonNestedField_ReturnsTrue()
    {
        // Arrange
        var output = CreateOutputWithResult("""{"metadata": {"category": "game_rules"}}""");

        // Act
        var result = _evaluator.Evaluate("output.metadata.category == 'game_rules'", output);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_JsonFieldBoolean_ReturnsTrue()
    {
        // Arrange
        var output = CreateOutputWithResult("""{"cached": true}""");

        // Act
        var result = _evaluator.Evaluate("output.cached == true", output);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Comparison Operator Tests

    [Theory]
    [InlineData("confidence > 0.5", 0.6, true)]
    [InlineData("confidence > 0.5", 0.5, false)]
    [InlineData("confidence >= 0.5", 0.5, true)]
    [InlineData("confidence < 0.5", 0.4, true)]
    [InlineData("confidence < 0.5", 0.5, false)]
    [InlineData("confidence <= 0.5", 0.5, true)]
    [InlineData("confidence != 0.5", 0.6, true)]
    [InlineData("confidence != 0.5", 0.5, false)]
    public void Evaluate_NumericComparisons_ReturnsExpected(string condition, double confidence, bool expected)
    {
        // Arrange
        var output = CreateOutputWithConfidence(confidence);

        // Act
        var result = _evaluator.Evaluate(condition, output);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void Evaluate_InvalidExpression_ReturnsTrue()
    {
        // Arrange - fail-open behavior for routing
        var output = CreateSuccessfulOutput();

        // Act
        var result = _evaluator.Evaluate("this is not a valid expression !!!", output);

        // Assert
        result.Should().BeTrue(); // Fail-open
    }

    [Fact]
    public void Evaluate_NullResult_HandlesMissingField()
    {
        // Arrange
        var output = new PluginOutput
        {
            ExecutionId = Guid.NewGuid(),
            Success = true,
            Result = null
        };

        // Act
        var result = _evaluator.Evaluate("output.type == 'rules'", output);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MissingJsonField_ReturnsFalse()
    {
        // Arrange
        var output = CreateOutputWithResult("""{"other": "value"}""");

        // Act
        var result = _evaluator.Evaluate("output.nonexistent == 'test'", output);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Case Insensitivity Tests

    [Fact]
    public void Evaluate_CaseInsensitiveKeyword_ReturnsTrue()
    {
        // Arrange
        var output = CreateSuccessfulOutput();

        // Act
        var result = _evaluator.Evaluate("AlWaYs", output);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_CaseInsensitiveStringComparison_ReturnsTrue()
    {
        // Arrange
        var output = CreateOutputWithResult("""{"type": "RULES"}""");

        // Act
        var result = _evaluator.Evaluate("output.type == 'rules'", output);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static PluginOutput CreateSuccessfulOutput()
    {
        return new PluginOutput
        {
            ExecutionId = Guid.NewGuid(),
            Success = true,
            Result = JsonDocument.Parse("{}")
        };
    }

    private static PluginOutput CreateOutputWithConfidence(double confidence)
    {
        return new PluginOutput
        {
            ExecutionId = Guid.NewGuid(),
            Success = true,
            Confidence = confidence,
            Result = JsonDocument.Parse("{}")
        };
    }

    private static PluginOutput CreateOutputWithResult(string json)
    {
        return new PluginOutput
        {
            ExecutionId = Guid.NewGuid(),
            Success = true,
            Result = JsonDocument.Parse(json)
        };
    }

    #endregion
}
