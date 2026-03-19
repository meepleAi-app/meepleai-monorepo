using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Tests for LedgerStateSchema, LedgerPlayerState, and LedgerStateChange value objects.
/// Issue #3025: Backend 90% Coverage Target - Phase 23
/// </summary>
[Trait("Category", "Unit")]
public sealed class LedgerStateSchemaTests
{
    #region LedgerStateSchema.CreateEmpty Tests

    [Fact]
    public void CreateEmpty_WithPlayerNames_CreatesSchemaWithPlayers()
    {
        // Arrange
        var players = new[] { "Alice", "Bob", "Charlie" };

        // Act
        var schema = LedgerStateSchema.CreateEmpty(players);

        // Assert
        schema.Players.Should().HaveCount(3);
        schema.Players.Should().ContainKeys("Alice", "Bob", "Charlie");
        schema.CurrentPhase.Should().Be("Setup");
        schema.CurrentRound.Should().Be(1);
        schema.History.Should().BeEmpty();
    }

    [Fact]
    public void CreateEmpty_AssignsTurnOrderCorrectly()
    {
        // Arrange
        var players = new[] { "Alice", "Bob", "Charlie" };

        // Act
        var schema = LedgerStateSchema.CreateEmpty(players);

        // Assert
        schema.Players["Alice"].TurnOrder.Should().Be(1);
        schema.Players["Alice"].IsCurrentTurn.Should().BeTrue();

        schema.Players["Bob"].TurnOrder.Should().Be(2);
        schema.Players["Bob"].IsCurrentTurn.Should().BeFalse();

        schema.Players["Charlie"].TurnOrder.Should().Be(3);
        schema.Players["Charlie"].IsCurrentTurn.Should().BeFalse();
    }

    [Fact]
    public void CreateEmpty_InitializesPlayersWithZeroScore()
    {
        // Arrange
        var players = new[] { "Player1" };

        // Act
        var schema = LedgerStateSchema.CreateEmpty(players);

        // Assert
        schema.Players["Player1"].Score.Should().Be(0);
        schema.Players["Player1"].Resources.Should().BeEmpty();
    }

    [Fact]
    public void CreateEmpty_WithEmptyPlayerList_CreatesSchemaWithNoPlayers()
    {
        // Act
        var schema = LedgerStateSchema.CreateEmpty(Array.Empty<string>());

        // Assert
        schema.Players.Should().BeEmpty();
    }

    [Fact]
    public void CreateEmpty_WithNullPlayerNames_ThrowsArgumentNullException()
    {
        // Act
        var action = () => LedgerStateSchema.CreateEmpty(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region LedgerStateSchema.ToJsonDocument Tests

    [Fact]
    public void ToJsonDocument_SerializesCorrectly()
    {
        // Arrange
        var schema = LedgerStateSchema.CreateEmpty(new[] { "Alice", "Bob" });

        // Act
        using var jsonDoc = schema.ToJsonDocument();
        var json = jsonDoc.RootElement.GetRawText();

        // Assert
        json.Should().Contain("\"currentPhase\"");
        json.Should().Contain("\"currentRound\"");
        json.Should().Contain("\"players\"");
        json.Should().Contain("Alice");
        json.Should().Contain("Bob");
    }

    [Fact]
    public void ToJsonDocument_UsesCorrectPropertyNames()
    {
        // Arrange
        var schema = LedgerStateSchema.CreateEmpty(new[] { "Player1" });

        // Act
        var root = jsonDoc.RootElement;

        // Assert - camelCase property names
        root.TryGetProperty("currentPhase", out _).Should().BeTrue();
        root.TryGetProperty("currentRound", out _).Should().BeTrue();
        root.TryGetProperty("players", out _).Should().BeTrue();
        root.TryGetProperty("history", out _).Should().BeTrue();
    }

    #endregion

    #region LedgerStateSchema.FromJsonDocument Tests

    [Fact]
    public void FromJsonDocument_DeserializesCorrectly()
    {
        // Arrange
        var originalSchema = LedgerStateSchema.CreateEmpty(new[] { "Alice", "Bob" });
        using var jsonDoc = originalSchema.ToJsonDocument();

        // Act
        var deserializedSchema = LedgerStateSchema.FromJsonDocument(jsonDoc);

        // Assert
        deserializedSchema.CurrentPhase.Should().Be(originalSchema.CurrentPhase);
        deserializedSchema.CurrentRound.Should().Be(originalSchema.CurrentRound);
        deserializedSchema.Players.Should().HaveCount(2);
        deserializedSchema.Players["Alice"].TurnOrder.Should().Be(1);
        deserializedSchema.Players["Bob"].TurnOrder.Should().Be(2);
    }

    [Fact]
    public void FromJsonDocument_WithNullDocument_ThrowsArgumentNullException()
    {
        // Act
        var action = () => LedgerStateSchema.FromJsonDocument(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Roundtrip_PreservesAllData()
    {
        // Arrange
        var schema = LedgerStateSchema.CreateEmpty(new[] { "Alice", "Bob", "Charlie" });

        // Act
        var roundtripped = LedgerStateSchema.FromJsonDocument(jsonDoc);

        // Assert
        roundtripped.CurrentPhase.Should().Be(schema.CurrentPhase);
        roundtripped.CurrentRound.Should().Be(schema.CurrentRound);
        roundtripped.Players.Should().HaveCount(schema.Players.Count);

        foreach (var (name, player) in schema.Players)
        {
            roundtripped.Players.Should().ContainKey(name);
            roundtripped.Players[name].Score.Should().Be(player.Score);
            roundtripped.Players[name].TurnOrder.Should().Be(player.TurnOrder);
            roundtripped.Players[name].IsCurrentTurn.Should().Be(player.IsCurrentTurn);
        }
    }

    #endregion

    #region LedgerPlayerState Tests

    [Fact]
    public void LedgerPlayerState_DefaultValues_AreCorrect()
    {
        // Act
        var player = new LedgerPlayerState();

        // Assert
        player.PlayerName.Should().BeEmpty();
        player.Score.Should().Be(0);
        player.Resources.Should().BeEmpty();
        player.TurnOrder.Should().Be(1);
        player.IsCurrentTurn.Should().BeFalse();
        player.CustomState.Should().BeNull();
    }

    [Fact]
    public void LedgerPlayerState_WithValues_SetsCorrectly()
    {
        // Act
        var player = new LedgerPlayerState
        {
            PlayerName = "Alice",
            Score = 42,
            Resources = new Dictionary<string, int> { { "gold", 10 }, { "wood", 5 } },
            TurnOrder = 2,
            IsCurrentTurn = true
        };

        // Assert
        player.PlayerName.Should().Be("Alice");
        player.Score.Should().Be(42);
        player.Resources.Should().HaveCount(2);
        player.Resources["gold"].Should().Be(10);
        player.Resources["wood"].Should().Be(5);
        player.TurnOrder.Should().Be(2);
        player.IsCurrentTurn.Should().BeTrue();
    }

    #endregion

    #region LedgerStateChange Tests

    [Fact]
    public void LedgerStateChange_DefaultValues_AreCorrect()
    {
        // Act
        var change = new LedgerStateChange();

        // Assert
        change.PlayerName.Should().BeNull();
        change.ChangeType.Should().BeEmpty();
        change.FieldName.Should().BeEmpty();
        change.OldValue.Should().BeNull();
        change.NewValue.Should().BeEmpty();
        change.Source.Should().Be("ledger-agent");
        change.IsConfirmed.Should().BeFalse();
    }

    [Fact]
    public void LedgerStateChange_WithValues_SetsCorrectly()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var change = new LedgerStateChange
        {
            PlayerName = "Alice",
            ChangeType = "score_update",
            FieldName = "score",
            OldValue = "10",
            NewValue = "15",
            Timestamp = timestamp,
            Source = "manual",
            IsConfirmed = true
        };

        // Assert
        change.PlayerName.Should().Be("Alice");
        change.ChangeType.Should().Be("score_update");
        change.FieldName.Should().Be("score");
        change.OldValue.Should().Be("10");
        change.NewValue.Should().Be("15");
        change.Timestamp.Should().Be(timestamp);
        change.Source.Should().Be("manual");
        change.IsConfirmed.Should().BeTrue();
    }

    [Fact]
    public void LedgerStateChange_Timestamp_DefaultsToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var change = new LedgerStateChange();

        // Assert
        change.Timestamp.Should().BeOnOrAfter(beforeCreation);
        change.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Complex Schema Tests

    [Fact]
    public void Schema_WithHistoryEntries_SerializesCorrectly()
    {
        // Arrange
        var schema = new LedgerStateSchema
        {
            CurrentPhase = "InProgress",
            CurrentRound = 3,
            Players = new Dictionary<string, LedgerPlayerState>
            {
                ["Alice"] = new LedgerPlayerState { PlayerName = "Alice", Score = 25, TurnOrder = 1 }
            },
            History = new List<LedgerStateChange>
            {
                new LedgerStateChange
                {
                    PlayerName = "Alice",
                    ChangeType = "score_update",
                    FieldName = "score",
                    OldValue = "20",
                    NewValue = "25",
                    IsConfirmed = true
                }
            }
        };

        // Act
        var restored = LedgerStateSchema.FromJsonDocument(jsonDoc);

        // Assert
        restored.History.Should().HaveCount(1);
        restored.History[0].PlayerName.Should().Be("Alice");
        restored.History[0].ChangeType.Should().Be("score_update");
        restored.History[0].NewValue.Should().Be("25");
    }

    [Fact]
    public void Schema_WithCustomState_HandlesCorrectly()
    {
        // Arrange
        var schema = new LedgerStateSchema
        {
            CurrentPhase = "Setup",
            CurrentRound = 1,
            Players = new Dictionary<string, LedgerPlayerState>(),
            CustomState = new Dictionary<string, object>
            {
                ["boardSetup"] = "completed",
                ["deckSize"] = 52
            }
        };

        // Act
        var json = jsonDoc.RootElement.GetRawText();

        // Assert
        json.Should().Contain("customState");
        json.Should().Contain("boardSetup");
    }

    #endregion
}
