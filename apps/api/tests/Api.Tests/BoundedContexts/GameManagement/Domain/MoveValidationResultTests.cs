using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Models;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class MoveValidationResultTests
{
    [Fact]
    public void Valid_WithValidParameters_CreatesValidResult()
    {
        // Arrange
        var rules = new List<RuleAtom>
        {
            new RuleAtom("rule1", "Test rule", "Section", "1")
        };

        // Act
        var result = MoveValidationResult.Valid(rules, 0.95);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Single(result.ApplicableRules);
        Assert.Equal(0.95, result.ConfidenceScore);
        Assert.Null(result.Suggestions);
    }

    [Fact]
    public void Valid_WithSuggestions_IncludesSuggestions()
    {
        // Arrange
        var rules = new List<RuleAtom>
        {
            new RuleAtom("rule1", "Test rule")
        };
        var suggestions = new List<string> { "Consider rolling again" };

        // Act
        var result = MoveValidationResult.Valid(rules, 0.8, suggestions);

        // Assert
        Assert.True(result.IsValid);
        Assert.Single(result.Suggestions!);
        Assert.Equal("Consider rolling again", result.Suggestions![0]);
    }

    [Fact]
    public void Valid_WithEmptyRules_CreatesValidResultWithNoRules()
    {
        // Arrange & Act
        var result = MoveValidationResult.Valid(Array.Empty<RuleAtom>());

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.ApplicableRules);
        Assert.Equal(1.0, result.ConfidenceScore); // Default confidence
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Valid_WithInvalidConfidence_ThrowsArgumentException(double confidence)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            MoveValidationResult.Valid(Array.Empty<RuleAtom>(), confidence));
        Assert.Contains("between 0.0 and 1.0", exception.Message);
    }
    [Fact]
    public void Invalid_WithValidParameters_CreatesInvalidResult()
    {
        // Arrange
        var errors = new List<string> { "Cannot move backwards" };
        var rules = new List<RuleAtom>
        {
            new RuleAtom("rule1", "Players cannot move backwards")
        };

        // Act
        var result = MoveValidationResult.Invalid(errors, rules, 0.9);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("Cannot move backwards", result.Errors[0]);
        Assert.Single(result.ApplicableRules);
        Assert.Equal(0.9, result.ConfidenceScore);
    }

    [Fact]
    public void Invalid_WithMultipleErrors_IncludesAllErrors()
    {
        // Arrange
        var errors = new List<string>
        {
            "Cannot move backwards",
            "Insufficient resources",
            "Wrong turn order"
        };

        // Act
        var result = MoveValidationResult.Invalid(errors, Array.Empty<RuleAtom>());

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Invalid_WithNullErrors_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            MoveValidationResult.Invalid(null!, Array.Empty<RuleAtom>()));
        Assert.Contains("at least one error", exception.Message);
    }

    [Fact]
    public void Invalid_WithEmptyErrors_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            MoveValidationResult.Invalid(Array.Empty<string>(), Array.Empty<RuleAtom>()));
        Assert.Contains("at least one error", exception.Message);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(1.5)]
    public void Invalid_WithInvalidConfidence_ThrowsArgumentException(double confidence)
    {
        // Arrange
        var errors = new List<string> { "Test error" };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            MoveValidationResult.Invalid(errors, Array.Empty<RuleAtom>(), confidence));
        Assert.Contains("between 0.0 and 1.0", exception.Message);
    }

    [Fact]
    public void Invalid_WithSuggestions_IncludesSuggestions()
    {
        // Arrange
        var errors = new List<string> { "Invalid move" };
        var suggestions = new List<string> { "Try moving forward instead" };

        // Act
        var result = MoveValidationResult.Invalid(errors, Array.Empty<RuleAtom>(), suggestions: suggestions);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Suggestions!);
    }
    [Fact]
    public void Uncertain_WithValidParameters_CreatesUncertainResult()
    {
        // Arrange
        var rules = new List<RuleAtom>
        {
            new RuleAtom("rule1", "Ambiguous rule text")
        };

        // Act
        var result = MoveValidationResult.Uncertain("Ambiguous rules", rules, 0.4);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("Uncertain: Ambiguous rules", result.Errors[0]);
        Assert.Single(result.ApplicableRules);
        Assert.Equal(0.4, result.ConfidenceScore);
        Assert.NotNull(result.Suggestions);
        Assert.Contains(result.Suggestions, s => s.Contains("Review game rules"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Uncertain_WithInvalidReason_ThrowsArgumentException(string? reason)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            MoveValidationResult.Uncertain(reason!, Array.Empty<RuleAtom>(), 0.5));
        Assert.Contains("must have a reason", exception.Message);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Uncertain_WithInvalidConfidence_ThrowsArgumentException(double confidence)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            MoveValidationResult.Uncertain("Test reason", Array.Empty<RuleAtom>(), confidence));
        Assert.Contains("between 0.0 and 1.0", exception.Message);
    }
    [Fact]
    public void MoveValidationResult_EqualityByValue_WorksCorrectly()
    {
        // Arrange
        var errors = new List<string> { "Error 1" };
        var rules = new List<RuleAtom> { new RuleAtom("rule1", "Test") };

        var result1 = MoveValidationResult.Invalid(errors, rules, 0.8);
        var result2 = MoveValidationResult.Invalid(errors, rules, 0.8);

        // Act & Assert
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void MoveValidationResult_DifferentIsValid_AreNotEqual()
    {
        // Arrange
        var rules = new List<RuleAtom> { new RuleAtom("rule1", "Test") };

        var result1 = MoveValidationResult.Valid(rules);
        var result2 = MoveValidationResult.Invalid(new[] { "Error" }, rules);

        // Act & Assert
        Assert.NotEqual(result1, result2);
    }
}

