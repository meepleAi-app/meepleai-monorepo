using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;
using FluentAssertions;

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
        result.Reason.Should().Be("Move is legal per Knight rules");
        result.ConfidenceScore.Should().Be(0.95);
        result.ExecutionTimeMs.Should().Be(42.5);
        result.AppliedRuleIds.Should().ContainSingle();
        result.Citations.Should().ContainSingle();
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
        result.Reason.Should().Contain("violates");
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Error_ShouldCreateErrorResult()
    {
        // Act
        var result = MoveValidationResult.Error("Network timeout", 150.0);

        // Assert
        Assert.False(result.IsValid);
        result.Reason.Should().Be("Validation error occurred");
        result.ErrorMessage.Should().Be("Network timeout");
        result.ConfidenceScore.Should().Be(0);
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
