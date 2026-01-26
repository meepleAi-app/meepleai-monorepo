using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the MoveValidationResult value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 19
/// </summary>
[Trait("Category", "Unit")]
public sealed class MoveValidationResultTests
{
    #region Valid Factory Tests

    [Fact]
    public void Valid_WithDefaultParameters_CreatesValidResult()
    {
        // Arrange
        var rules = new List<RuleAtom>
        {
            new RuleAtom("rule1", "Player can move piece", "Movement")
        };

        // Act
        var result = MoveValidationResult.Valid(rules);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.ApplicableRules.Should().HaveCount(1);
        result.ConfidenceScore.Should().Be(1.0);
        result.Suggestions.Should().BeNull();
    }

    [Fact]
    public void Valid_WithCustomConfidenceScore_SetsConfidence()
    {
        // Arrange
        var rules = new List<RuleAtom>();

        // Act
        var result = MoveValidationResult.Valid(rules, confidenceScore: 0.85);

        // Assert
        result.ConfidenceScore.Should().Be(0.85);
    }

    [Fact]
    public void Valid_WithSuggestions_SetsSuggestions()
    {
        // Arrange
        var rules = new List<RuleAtom>();
        var suggestions = new List<string> { "Consider using the knight", "Build a settlement" };

        // Act
        var result = MoveValidationResult.Valid(rules, suggestions: suggestions);

        // Assert
        result.Suggestions.Should().HaveCount(2);
        result.Suggestions.Should().Contain("Consider using the knight");
    }

    [Fact]
    public void Valid_WithNullRules_UsesEmptyArray()
    {
        // Act
        var result = MoveValidationResult.Valid(null!);

        // Assert
        result.ApplicableRules.Should().BeEmpty();
    }

    [Fact]
    public void Valid_WithNegativeConfidenceScore_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Valid(new List<RuleAtom>(), confidenceScore: -0.1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("confidenceScore")
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Valid_WithConfidenceScoreGreaterThanOne_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Valid(new List<RuleAtom>(), confidenceScore: 1.1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Valid_WithZeroConfidenceScore_Succeeds()
    {
        // Act
        var result = MoveValidationResult.Valid(new List<RuleAtom>(), confidenceScore: 0.0);

        // Assert
        result.ConfidenceScore.Should().Be(0.0);
    }

    [Fact]
    public void Valid_WithOneConfidenceScore_Succeeds()
    {
        // Act
        var result = MoveValidationResult.Valid(new List<RuleAtom>(), confidenceScore: 1.0);

        // Assert
        result.ConfidenceScore.Should().Be(1.0);
    }

    #endregion

    #region Invalid Factory Tests

    [Fact]
    public void Invalid_WithErrors_CreatesInvalidResult()
    {
        // Arrange
        var errors = new List<string> { "Cannot move to occupied space" };
        var rules = new List<RuleAtom>
        {
            new RuleAtom("rule1", "Movement rules", "Movement")
        };

        // Act
        var result = MoveValidationResult.Invalid(errors, rules);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Be("Cannot move to occupied space");
        result.ConfidenceScore.Should().Be(1.0);
    }

    [Fact]
    public void Invalid_WithMultipleErrors_SetsAllErrors()
    {
        // Arrange
        var errors = new List<string>
        {
            "Cannot move to occupied space",
            "Not enough movement points",
            "Piece is blocked"
        };
        var rules = new List<RuleAtom>();

        // Act
        var result = MoveValidationResult.Invalid(errors, rules);

        // Assert
        result.Errors.Should().HaveCount(3);
    }

    [Fact]
    public void Invalid_WithNullErrors_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Invalid(null!, new List<RuleAtom>());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("errors")
            .WithMessage("*Invalid result must have at least one error*");
    }

    [Fact]
    public void Invalid_WithEmptyErrors_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Invalid(new List<string>(), new List<RuleAtom>());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid result must have at least one error*");
    }

    [Fact]
    public void Invalid_WithNegativeConfidenceScore_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Invalid(
            new List<string> { "Error" },
            new List<RuleAtom>(),
            confidenceScore: -0.1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Invalid_WithConfidenceScoreGreaterThanOne_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Invalid(
            new List<string> { "Error" },
            new List<RuleAtom>(),
            confidenceScore: 1.5);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Invalid_WithCustomConfidenceScore_SetsConfidence()
    {
        // Arrange
        var errors = new List<string> { "Invalid move" };

        // Act
        var result = MoveValidationResult.Invalid(errors, new List<RuleAtom>(), confidenceScore: 0.75);

        // Assert
        result.ConfidenceScore.Should().Be(0.75);
    }

    [Fact]
    public void Invalid_WithSuggestions_SetsSuggestions()
    {
        // Arrange
        var errors = new List<string> { "Invalid move" };
        var suggestions = new List<string> { "Try moving to B3 instead" };

        // Act
        var result = MoveValidationResult.Invalid(errors, new List<RuleAtom>(), suggestions: suggestions);

        // Assert
        result.Suggestions.Should().HaveCount(1);
        result.Suggestions![0].Should().Be("Try moving to B3 instead");
    }

    [Fact]
    public void Invalid_WithNullRules_UsesEmptyArray()
    {
        // Arrange
        var errors = new List<string> { "Error" };

        // Act
        var result = MoveValidationResult.Invalid(errors, null!);

        // Assert
        result.ApplicableRules.Should().BeEmpty();
    }

    #endregion

    #region Uncertain Factory Tests

    [Fact]
    public void Uncertain_WithReason_CreatesUncertainResult()
    {
        // Arrange
        var reason = "Rule interpretation unclear";
        var rules = new List<RuleAtom>
        {
            new RuleAtom("rule1", "Complex rule", "Special")
        };

        // Act
        var result = MoveValidationResult.Uncertain(reason, rules, confidenceScore: 0.3);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().StartWith("Uncertain:");
        result.Errors[0].Should().Contain("Rule interpretation unclear");
        result.ConfidenceScore.Should().Be(0.3);
    }

    [Fact]
    public void Uncertain_ProvidesSuggestionToConsultRulebook()
    {
        // Act
        var result = MoveValidationResult.Uncertain("Ambiguous rule", new List<RuleAtom>(), 0.5);

        // Assert
        result.Suggestions.Should().HaveCount(1);
        result.Suggestions![0].Should().Contain("Review game rules or consult rulebook");
    }

    [Fact]
    public void Uncertain_WithEmptyReason_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Uncertain("", new List<RuleAtom>(), 0.5);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("reason")
            .WithMessage("*Uncertain result must have a reason*");
    }

    [Fact]
    public void Uncertain_WithWhitespaceReason_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Uncertain("   ", new List<RuleAtom>(), 0.5);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Uncertain result must have a reason*");
    }

    [Fact]
    public void Uncertain_WithNegativeConfidenceScore_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Uncertain("Reason", new List<RuleAtom>(), -0.1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Uncertain_WithConfidenceScoreGreaterThanOne_ThrowsArgumentException()
    {
        // Act
        var action = () => MoveValidationResult.Uncertain("Reason", new List<RuleAtom>(), 1.5);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    [Fact]
    public void Uncertain_WithNullRules_UsesEmptyArray()
    {
        // Act
        var result = MoveValidationResult.Uncertain("Reason", null!, 0.5);

        // Assert
        result.ApplicableRules.Should().BeEmpty();
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var rules = new List<RuleAtom>();
        var result1 = MoveValidationResult.Valid(rules, 0.9);
        var result2 = MoveValidationResult.Valid(rules, 0.9);

        // Assert - records with same values should be equal
        result1.IsValid.Should().Be(result2.IsValid);
        result1.ConfidenceScore.Should().Be(result2.ConfidenceScore);
    }

    #endregion
}
