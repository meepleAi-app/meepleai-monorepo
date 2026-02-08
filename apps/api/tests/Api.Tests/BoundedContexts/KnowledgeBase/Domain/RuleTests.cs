using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

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
        Assert.Equal(id, rule.Id);
        Assert.Equal(gameId, rule.GameId);
        Assert.Equal(ruleName, rule.RuleName);
        Assert.Equal(description, rule.Description);
        Assert.Equal(ruleType, rule.Type);
        Assert.Equal(10, rule.PrecedenceLevel);
        Assert.True(rule.IsActive);
    }

    [Fact]
    public void Rule_Create_WithEmptyName_ShouldThrowArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Rule(
                Guid.NewGuid(), Guid.NewGuid(), "", "desc",
                RuleType.Movement, 10, "context", "pattern"
            )
        );
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
        Assert.Equal("Updated description", rule.Description);
        Assert.Equal("new pattern", rule.ValidationPattern);
        Assert.Equal(20, rule.PrecedenceLevel);
        Assert.NotNull(rule.UpdatedAt);
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
        Assert.True(rule.IsActive);
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
        Assert.False(rule.IsActive);
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
        Assert.True(rule.AppliesTo("knight"));
        Assert.True(rule.AppliesTo("BISHOP")); // Case insensitive
        Assert.False(rule.AppliesTo("pawn"));
    }
}
