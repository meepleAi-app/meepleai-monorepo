using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for Rule aggregate root.
/// Issue #3759: Rules Arbitration Engine
/// </summary>
public class RuleTests
{
    [Fact]
    public void Rule_Create_ShouldInitializeWithValidProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var ruleName = "Knight L-Shape Movement";
        var description = "Knights move in L-shape pattern";
        var ruleType = RuleType.Movement;

        // Act
        var rule = new Rule(
            id, gameId, ruleName, description, ruleType,
            precedenceLevel: 10,
            applicableContext: "knight",
            validationPattern: "^N[a-h][1-8]$"
        );

        // Assert
        rule.Id.Should().Be(id);
        rule.GameId.Should().Be(gameId);
        rule.RuleName.Should().Be(ruleName);
        rule.Description.Should().Be(description);
        rule.Type.Should().Be(ruleType);
        rule.PrecedenceLevel.Should().Be(10);
        rule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Rule_Create_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Action act = () =>
            new Rule(
                Guid.NewGuid(), Guid.NewGuid(), "", "desc",
                RuleType.Movement, 10, "context", "pattern"
            );
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rule_Update_ShouldModifyDescription()
    {
        // Arrange
        var rule = new Rule(
            Guid.NewGuid(), Guid.NewGuid(), "Test", "Original",
            RuleType.Movement, 10, "", ""
        );

        // Act
        rule.Update("Updated description", "new pattern", 20);

        // Assert
        rule.Description.Should().Be("Updated description");
        rule.ValidationPattern.Should().Be("new pattern");
        rule.PrecedenceLevel.Should().Be(20);
        rule.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Rule_Activate_ShouldSetIsActiveTrue()
    {
        // Arrange
        var rule = new Rule(
            Guid.NewGuid(), Guid.NewGuid(), "Test", "desc",
            RuleType.Movement, 10, "", "", isActive: false
        );

        // Act
        rule.Activate();

        // Assert
        rule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Rule_Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var rule = new Rule(
            Guid.NewGuid(), Guid.NewGuid(), "Test", "desc",
            RuleType.Movement, 10, "", ""
        );

        // Act
        rule.Deactivate();

        // Assert
        rule.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Rule_AppliesTo_ShouldMatchContext()
    {
        // Arrange
        var rule = new Rule(
            Guid.NewGuid(), Guid.NewGuid(), "Test", "desc",
            RuleType.Movement, 10, "knight,bishop", ""
        );

        // Act & Assert
        rule.AppliesTo("knight").Should().BeTrue();
        rule.AppliesTo("BISHOP").Should().BeTrue(); // Case insensitive
        rule.AppliesTo("pawn").Should().BeFalse();
    }
}
