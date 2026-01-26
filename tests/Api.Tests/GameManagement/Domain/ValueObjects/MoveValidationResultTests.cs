using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.GameManagement.Domain.ValueObjects;

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
            new("R1", "Rule text 1", "Section 1", "1", "10")
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
    public void Valid_WithCustomConfidence_SetsConfidenceScore()
    {
        // Arrange
        var rules = Array.Empty<RuleAtom>();

        // Act
        var result = MoveValidationResult.Valid(rules, confidenceScore: 0.85);

        // Assert
        result.ConfidenceScore.Should().Be(0.85);
    }

    [Fact]
    public void Valid_WithSuggestions_SetsSuggestions()
    {
        // Arrange
        var rules = Array.Empty<RuleAtom>();
        var suggestions = new List<string> { "Consider moving to A5", "Watch for opponent's response" };

        // Act
        var result = MoveValidationResult.Valid(rules, suggestions: suggestions);

        // Assert
        result.Suggestions.Should().HaveCount(2);
        result.Suggestions.Should().Contain("Consider moving to A5");
    }

    [Fact]
    public void Valid_WithNullRules_UsesEmptyArray()
    {
        // Act
        var result = MoveValidationResult.Valid(null!);

        // Assert
        result.ApplicableRules.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    [InlineData(-1.0)]
    public void Valid_WithInvalidConfidence_ThrowsArgumentException(double invalidConfidence)
    {
        // Act & Assert
        var action = () => MoveValidationResult.Valid(Array.Empty<RuleAtom>(), invalidConfidence);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Valid_WithBoundaryConfidence_Succeeds(double validConfidence)
    {
        // Act
        var result = MoveValidationResult.Valid(Array.Empty<RuleAtom>(), validConfidence);

        // Assert
        result.ConfidenceScore.Should().Be(validConfidence);
    }

    #endregion

    #region Invalid Factory Tests

    [Fact]
    public void Invalid_WithErrors_CreatesInvalidResult()
    {
        // Arrange
        var errors = new[] { "Invalid move", "Out of bounds" };
        var rules = new List<RuleAtom>
        {
            new("R1", "You cannot move there")
        };

        // Act
        var result = MoveValidationResult.Invalid(errors, rules);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain("Invalid move");
        result.Errors.Should().Contain("Out of bounds");
        result.ApplicableRules.Should().HaveCount(1);
    }

    [Fact]
    public void Invalid_WithSuggestions_SetsSuggestions()
    {
        // Arrange
        var errors = new[] { "Invalid move" };
        var suggestions = new[] { "Try moving diagonally instead" };

        // Act
        var result = MoveValidationResult.Invalid(errors, Array.Empty<RuleAtom>(), 0.9, suggestions);

        // Assert
        result.Suggestions.Should().HaveCount(1);
        result.Suggestions.Should().Contain("Try moving diagonally instead");
    }

    [Fact]
    public void Invalid_WithNullErrors_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => MoveValidationResult.Invalid(null!, Array.Empty<RuleAtom>());
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid result must have at least one error*");
    }

    [Fact]
    public void Invalid_WithEmptyErrors_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => MoveValidationResult.Invalid(Array.Empty<string>(), Array.Empty<RuleAtom>());
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid result must have at least one error*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Invalid_WithInvalidConfidence_ThrowsArgumentException(double invalidConfidence)
    {
        // Act & Assert
        var action = () => MoveValidationResult.Invalid(
            new[] { "Error" },
            Array.Empty<RuleAtom>(),
            invalidConfidence);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    #endregion

    #region Uncertain Factory Tests

    [Fact]
    public void Uncertain_WithReason_CreatesUncertainResult()
    {
        // Arrange
        var reason = "Ambiguous rule interpretation";
        var rules = new List<RuleAtom>
        {
            new("R1", "Complex rule text"),
            new("R2", "Another complex rule")
        };

        // Act
        var result = MoveValidationResult.Uncertain(reason, rules, 0.4);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain(reason);
        result.ConfidenceScore.Should().Be(0.4);
        result.ApplicableRules.Should().HaveCount(2);
    }

    [Fact]
    public void Uncertain_IncludesDefaultSuggestion()
    {
        // Act
        var result = MoveValidationResult.Uncertain("Unclear rules", Array.Empty<RuleAtom>(), 0.3);

        // Assert
        result.Suggestions.Should().ContainSingle()
            .Which.Should().Contain("Review game rules");
    }

    [Fact]
    public void Uncertain_FormatsErrorWithPrefix()
    {
        // Act
        var result = MoveValidationResult.Uncertain("Cannot determine validity", Array.Empty<RuleAtom>(), 0.5);

        // Assert
        result.Errors.Should().ContainSingle()
            .Which.Should().StartWith("Uncertain:");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Uncertain_WithInvalidReason_ThrowsArgumentException(string? invalidReason)
    {
        // Act & Assert
        var action = () => MoveValidationResult.Uncertain(invalidReason!, Array.Empty<RuleAtom>(), 0.5);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Uncertain result must have a reason*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Uncertain_WithInvalidConfidence_ThrowsArgumentException(double invalidConfidence)
    {
        // Act & Assert
        var action = () => MoveValidationResult.Uncertain("Reason", Array.Empty<RuleAtom>(), invalidConfidence);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Confidence score must be between 0.0 and 1.0*");
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equals_TwoIdenticalValidResults_AreEqual()
    {
        // Arrange
        var rules = new List<RuleAtom> { new("R1", "Text") };
        var result1 = MoveValidationResult.Valid(rules, 0.9);
        var result2 = MoveValidationResult.Valid(rules, 0.9);

        // Act & Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void Equals_ResultsWithDifferentIsValid_AreNotEqual()
    {
        // Arrange
        var validResult = MoveValidationResult.Valid(Array.Empty<RuleAtom>());
        var invalidResult = MoveValidationResult.Invalid(new[] { "Error" }, Array.Empty<RuleAtom>());

        // Act & Assert
        validResult.Should().NotBe(invalidResult);
    }

    [Fact]
    public void Equals_ResultsWithDifferentConfidence_AreNotEqual()
    {
        // Arrange
        var result1 = MoveValidationResult.Valid(Array.Empty<RuleAtom>(), 0.8);
        var result2 = MoveValidationResult.Valid(Array.Empty<RuleAtom>(), 0.9);

        // Act & Assert
        result1.Should().NotBe(result2);
    }

    #endregion

    #region RuleAtom Coverage Tests

    [Fact]
    public void Valid_WithMultipleRules_PreservesRuleDetails()
    {
        // Arrange
        var rules = new List<RuleAtom>
        {
            new("R1", "First rule", "Movement", "1", "1"),
            new("R2", "Second rule", "Combat", "2", "5"),
            new("R3", "Third rule", null, null, null)
        };

        // Act
        var result = MoveValidationResult.Valid(rules);

        // Assert
        result.ApplicableRules.Should().HaveCount(3);
        result.ApplicableRules[0].id.Should().Be("R1");
        result.ApplicableRules[0].section.Should().Be("Movement");
        result.ApplicableRules[1].page.Should().Be("2");
        result.ApplicableRules[2].section.Should().BeNull();
    }

    #endregion
}
