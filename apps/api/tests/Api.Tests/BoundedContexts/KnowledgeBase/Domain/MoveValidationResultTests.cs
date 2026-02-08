using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for MoveValidationResult value object.
/// Issue #3759: Rules Arbitration Engine
/// </summary>
public class MoveValidationResultTests
{
    [Fact]
    public void Valid_ShouldCreateValidResult()
    {
        // Arrange
        var ruleIds = new List<Guid> { Guid.NewGuid() };
        var citations = new List<string> { "rule.pdf#L10" };

        // Act
        var result = MoveValidationResult.Valid(
            "Move is legal per Knight rules",
            ruleIds,
            0.95,
            citations,
            42.5
        );

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("Move is legal per Knight rules", result.Reason);
        Assert.Equal(0.95, result.ConfidenceScore);
        Assert.Equal(42.5, result.ExecutionTimeMs);
        Assert.Single(result.AppliedRuleIds);
        Assert.Single(result.Citations);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Invalid_ShouldCreateInvalidResult()
    {
        // Arrange
        var ruleIds = new List<Guid> { Guid.NewGuid() };

        // Act
        var result = MoveValidationResult.Invalid(
            "Move violates Knight movement rules",
            ruleIds,
            0.92,
            new List<string>(),
            38.2
        );

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("violates", result.Reason);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Error_ShouldCreateErrorResult()
    {
        // Act
        var result = MoveValidationResult.Error("Network timeout", 150.0);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Validation error occurred", result.Reason);
        Assert.Equal("Network timeout", result.ErrorMessage);
        Assert.Equal(0, result.ConfidenceScore);
        Assert.Empty(result.AppliedRuleIds);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public void Valid_WithInvalidConfidence_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MoveValidationResult.Valid("reason", new List<Guid>(), 1.5, new List<string>(), 10)
        );
    }
}
