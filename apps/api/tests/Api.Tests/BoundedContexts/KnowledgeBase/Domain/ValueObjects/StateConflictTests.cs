using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Unit tests for StateConflict value object.
/// Issue #2468 - Ledger Mode Test Suite
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class StateConflictTests
{
    #region Create Method Tests

    [Fact]
    public void Create_WithValidInputs_CreatesInstance()
    {
        // Arrange
        var lastUpdated = DateTime.UtcNow.AddMinutes(-10);

        // Act
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "ho 10 punti",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: lastUpdated,
            severity: ConflictSeverity.High);

        // Assert
        conflict.PropertyName.Should().Be("score");
        conflict.ConflictingMessage.Should().Be("ho 10 punti");
        conflict.ExistingValue.Should().Be(5);
        conflict.NewValue.Should().Be(10);
        conflict.LastUpdatedAt.Should().Be(lastUpdated);
        conflict.Severity.Should().Be(ConflictSeverity.High);
    }

    [Fact]
    public void Create_WithPlayerName_TrimsAndStoresPlayerName()
    {
        // Act
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "Marco ha 10 punti",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-10),
            severity: ConflictSeverity.High,
            playerName: "  Marco  ");

        // Assert
        conflict.PlayerName.Should().Be("Marco");
    }

    [Fact]
    public void Create_WithNullPlayerName_AllowsNull()
    {
        // Act
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "ho 10 punti",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-10),
            severity: ConflictSeverity.High,
            playerName: null);

        // Assert
        conflict.PlayerName.Should().BeNull();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithEmptyPropertyName_ThrowsArgumentException()
    {
        // Act
        var act = () => StateConflict.Create(
            propertyName: "",
            conflictingMessage: "test",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow,
            severity: ConflictSeverity.Medium);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Property name cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespacePropertyName_ThrowsArgumentException()
    {
        // Act
        var act = () => StateConflict.Create(
            propertyName: "   ",
            conflictingMessage: "test",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow,
            severity: ConflictSeverity.Medium);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Property name cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyConflictingMessage_ThrowsArgumentException()
    {
        // Act
        var act = () => StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow,
            severity: ConflictSeverity.Medium);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Conflicting message cannot be empty*");
    }

    #endregion

    #region Resolution Strategy Tests

    [Fact]
    public void Create_WithCriticalSeverity_SetsAskUserStrategy()
    {
        // Act
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "test",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-10),
            severity: ConflictSeverity.Critical);

        // Assert
        conflict.SuggestedResolution.Should().Be(ConflictResolutionStrategy.AskUser);
    }

    [Fact]
    public void Create_WithHighSeverity_SetsAskUserStrategy()
    {
        // Act
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "test",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-10),
            severity: ConflictSeverity.High);

        // Assert
        conflict.SuggestedResolution.Should().Be(ConflictResolutionStrategy.AskUser);
    }

    [Fact]
    public void Create_WithMediumSeverity_SetsPreferNewestStrategy()
    {
        // Act
        var conflict = StateConflict.Create(
            propertyName: "roads",
            conflictingMessage: "test",
            existingValue: 2,
            newValue: 3,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-10),
            severity: ConflictSeverity.Medium);

        // Assert
        conflict.SuggestedResolution.Should().Be(ConflictResolutionStrategy.PreferNewest);
    }

    [Fact]
    public void Create_WithLowSeverity_SetsPreferNewestStrategy()
    {
        // Act
        var conflict = StateConflict.Create(
            propertyName: "resources",
            conflictingMessage: "test",
            existingValue: 1,
            newValue: 2,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-10),
            severity: ConflictSeverity.Low);

        // Assert
        conflict.SuggestedResolution.Should().Be(ConflictResolutionStrategy.PreferNewest);
    }

    [Fact]
    public void Create_WithVeryRecentUpdate_SetsAskUserStrategy()
    {
        // Recent update (less than 30 seconds) always asks user
        var conflict = StateConflict.Create(
            propertyName: "roads",
            conflictingMessage: "test",
            existingValue: 2,
            newValue: 3,
            lastUpdatedAt: DateTime.UtcNow.AddSeconds(-5), // Very recent
            severity: ConflictSeverity.Low);

        // Assert
        conflict.SuggestedResolution.Should().Be(ConflictResolutionStrategy.AskUser);
    }

    #endregion

    #region FormatForDisplay Tests

    [Fact]
    public void FormatForDisplay_WithPlayerName_IncludesPlayerName()
    {
        // Arrange
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "Marco ha 10 punti",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5),
            severity: ConflictSeverity.High,
            playerName: "Marco");

        // Act
        var formatted = conflict.FormatForDisplay();

        // Assert
        formatted.Should().Contain("Conflitto rilevato");
        formatted.Should().Contain("Marco");
        formatted.Should().Contain("score");
        formatted.Should().Contain("5");
        formatted.Should().Contain("10");
    }

    [Fact]
    public void FormatForDisplay_WithoutPlayerName_ShowsPropertyOnly()
    {
        // Arrange
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "ho 10 punti",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5),
            severity: ConflictSeverity.High);

        // Act
        var formatted = conflict.FormatForDisplay();

        // Assert
        formatted.Should().Contain("Conflitto rilevato");
        formatted.Should().Contain("score");
        formatted.Should().Contain("Proprietà");
    }

    [Fact]
    public void FormatForDisplay_WithNullExistingValue_ShowsNonImpostato()
    {
        // Arrange
        var conflict = StateConflict.Create(
            propertyName: "roads",
            conflictingMessage: "test",
            existingValue: null,
            newValue: 3,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5),
            severity: ConflictSeverity.Medium);

        // Act
        var formatted = conflict.FormatForDisplay();

        // Assert
        formatted.Should().Contain("non impostato");
    }

    [Fact]
    public void FormatForDisplay_WithRecentUpdate_ShowsMinutesAgo()
    {
        // Arrange
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "test",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5),
            severity: ConflictSeverity.High);

        // Act
        var formatted = conflict.FormatForDisplay();

        // Assert
        formatted.Should().Contain("minuti fa");
    }

    [Fact]
    public void FormatForDisplay_WithVeryRecentUpdate_ShowsMenoUnMinuto()
    {
        // Arrange
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "test",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddSeconds(-30),
            severity: ConflictSeverity.High);

        // Act
        var formatted = conflict.FormatForDisplay();

        // Assert
        formatted.Should().Contain("meno di un minuto fa");
    }

    [Fact]
    public void FormatForDisplay_WithOldUpdate_ShowsHoursAgo()
    {
        // Arrange
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "test",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddHours(-2),
            severity: ConflictSeverity.High);

        // Act
        var formatted = conflict.FormatForDisplay();

        // Assert
        formatted.Should().Contain("ore fa");
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void ConflictSeverity_Low_HasValue0()
    {
        ConflictSeverity.Low.Should().Be((ConflictSeverity)0);
    }

    [Fact]
    public void ConflictSeverity_Medium_HasValue1()
    {
        ConflictSeverity.Medium.Should().Be((ConflictSeverity)1);
    }

    [Fact]
    public void ConflictSeverity_High_HasValue2()
    {
        ConflictSeverity.High.Should().Be((ConflictSeverity)2);
    }

    [Fact]
    public void ConflictSeverity_Critical_HasValue3()
    {
        ConflictSeverity.Critical.Should().Be((ConflictSeverity)3);
    }

    [Fact]
    public void ConflictResolutionStrategy_AskUser_HasValue0()
    {
        ConflictResolutionStrategy.AskUser.Should().Be((ConflictResolutionStrategy)0);
    }

    [Fact]
    public void ConflictResolutionStrategy_PreferNewest_HasValue1()
    {
        ConflictResolutionStrategy.PreferNewest.Should().Be((ConflictResolutionStrategy)1);
    }

    [Fact]
    public void ConflictResolutionStrategy_KeepExisting_HasValue2()
    {
        ConflictResolutionStrategy.KeepExisting.Should().Be((ConflictResolutionStrategy)2);
    }

    [Fact]
    public void ConflictResolutionStrategy_Merge_HasValue3()
    {
        ConflictResolutionStrategy.Merge.Should().Be((ConflictResolutionStrategy)3);
    }

    #endregion

    #region Value Comparison Edge Cases

    [Fact]
    public void Create_WithIntegerValues_StoresCorrectly()
    {
        // Act
        var conflict = StateConflict.Create(
            propertyName: "score",
            conflictingMessage: "test",
            existingValue: 5,
            newValue: 10,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5),
            severity: ConflictSeverity.High);

        // Assert
        conflict.ExistingValue.Should().Be(5);
        conflict.NewValue.Should().Be(10);
    }

    [Fact]
    public void Create_WithDoubleValues_StoresCorrectly()
    {
        // Act
        var conflict = StateConflict.Create(
            propertyName: "percentage",
            conflictingMessage: "test",
            existingValue: 5.5,
            newValue: 10.5,
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5),
            severity: ConflictSeverity.Medium);

        // Assert
        conflict.ExistingValue.Should().Be(5.5);
        conflict.NewValue.Should().Be(10.5);
    }

    [Fact]
    public void Create_WithStringValues_StoresCorrectly()
    {
        // Act
        var conflict = StateConflict.Create(
            propertyName: "currentPlayer",
            conflictingMessage: "test",
            existingValue: "Marco",
            newValue: "Luca",
            lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5),
            severity: ConflictSeverity.Medium);

        // Assert
        conflict.ExistingValue.Should().Be("Marco");
        conflict.NewValue.Should().Be("Luca");
    }

    #endregion
}
