using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

/// <summary>
/// Unit tests for <see cref="PlayRecordOutcomeCalculator"/>.
/// Tests the pure static helper that computes outcome fields from EF entities.
/// Issue #1663: Phase 1 – reskin-required fields computed on read.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "1663")]
public class PlayRecordOutcomeCalculatorTests
{
    #region WinnerPlayerIds

    [Fact]
    public void WinnerPlayerIds_OnePlayerWithWins1_ReturnsOnlyThatPlayer()
    {
        // Arrange
        var winner = MakePlayer(Guid.NewGuid(), ("wins", 1));
        var loser = MakePlayer(Guid.NewGuid(), ("wins", 0));
        var noScore = MakePlayer(Guid.NewGuid());

        // Act
        var result = PlayRecordOutcomeCalculator.WinnerPlayerIds([winner, loser, noScore]);

        // Assert
        result.Should().ContainSingle().Which.Should().Be(winner.Id);
    }

    [Fact]
    public void WinnerPlayerIds_TwoPlayersWithWins1_ReturnsBothIds()
    {
        // Arrange — tie: two winners
        var player1 = MakePlayer(Guid.NewGuid(), ("wins", 1));
        var player2 = MakePlayer(Guid.NewGuid(), ("wins", 1));

        // Act
        var result = PlayRecordOutcomeCalculator.WinnerPlayerIds([player1, player2]);

        // Assert
        result.Should().HaveCount(2)
            .And.Contain(player1.Id)
            .And.Contain(player2.Id);
    }

    [Fact]
    public void WinnerPlayerIds_NoPlayerHasWinsDimension_ReturnsEmpty()
    {
        // Arrange
        var player1 = MakePlayer(Guid.NewGuid(), ("points", 42));
        var player2 = MakePlayer(Guid.NewGuid(), ("points", 30));

        // Act
        var result = PlayRecordOutcomeCalculator.WinnerPlayerIds([player1, player2]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void WinnerPlayerIds_WinsDimensionExistsButValue0_NotIncluded()
    {
        // Arrange — "wins" dimension present but value = 0 (did not win)
        var player = MakePlayer(Guid.NewGuid(), ("wins", 0));

        // Act
        var result = PlayRecordOutcomeCalculator.WinnerPlayerIds([player]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void WinnerPlayerIds_WinsDimensionCaseInsensitive_Detected()
    {
        // Arrange — dimension stored as "Wins" (mixed case)
        var player = MakePlayerWithRawDimension(Guid.NewGuid(), "Wins", 1);

        // Act
        var result = PlayRecordOutcomeCalculator.WinnerPlayerIds([player]);

        // Assert — must be detected regardless of casing
        result.Should().ContainSingle().Which.Should().Be(player.Id);
    }

    [Fact]
    public void WinnerPlayerIds_EmptyPlayerList_ReturnsEmpty()
    {
        // Act
        var result = PlayRecordOutcomeCalculator.WinnerPlayerIds([]);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void WinnerPlayerIds_NullArgument_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => PlayRecordOutcomeCalculator.WinnerPlayerIds(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region OutcomeType

    [Fact]
    public void OutcomeType_AnyPlayerHasWinsDimension_ReturnsCompetitive()
    {
        // Arrange — presence of "wins" dimension = competitive game, regardless of value
        var player1 = MakePlayer(Guid.NewGuid(), ("wins", 1));
        var player2 = MakePlayer(Guid.NewGuid(), ("wins", 0));

        // Act
        var result = PlayRecordOutcomeCalculator.OutcomeType([player1, player2]);

        // Assert
        result.Should().Be("competitive");
    }

    [Fact]
    public void OutcomeType_NoPlayerHasWinsDimension_ReturnsNone()
    {
        // Arrange — cooperative / narrative / unscored game
        var player1 = MakePlayer(Guid.NewGuid(), ("points", 10));
        var player2 = MakePlayer(Guid.NewGuid());

        // Act
        var result = PlayRecordOutcomeCalculator.OutcomeType([player1, player2]);

        // Assert
        result.Should().Be("none");
    }

    [Fact]
    public void OutcomeType_WinsDimensionOnOnlyOnePlayer_ReturnsCompetitive()
    {
        // Arrange — only one player has the "wins" dimension (enough to mark as competitive)
        var winner = MakePlayer(Guid.NewGuid(), ("wins", 1));
        var noScore = MakePlayer(Guid.NewGuid());

        // Act
        var result = PlayRecordOutcomeCalculator.OutcomeType([winner, noScore]);

        // Assert
        result.Should().Be("competitive");
    }

    [Fact]
    public void OutcomeType_EmptyPlayerList_ReturnsNone()
    {
        // Act
        var result = PlayRecordOutcomeCalculator.OutcomeType([]);

        // Assert
        result.Should().Be("none");
    }

    [Fact]
    public void OutcomeType_InProgressRecordNoWinsDimension_ReturnsNone()
    {
        // Arrange — in-progress record: players exist but no wins dimension yet
        var player1 = MakePlayer(Guid.NewGuid());
        var player2 = MakePlayer(Guid.NewGuid());

        // Act
        var result = PlayRecordOutcomeCalculator.OutcomeType([player1, player2]);

        // Assert
        result.Should().Be("none");
    }

    [Fact]
    public void OutcomeType_NullArgument_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => PlayRecordOutcomeCalculator.OutcomeType(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region TotalScore

    [Fact]
    public void TotalScore_PlayerHasPointsDimension_ReturnsValue()
    {
        // Arrange
        var player = MakePlayer(Guid.NewGuid(), ("points", 42));

        // Act
        var result = PlayRecordOutcomeCalculator.TotalScore(player);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void TotalScore_PlayerHasNoPointsDimension_ReturnsNull()
    {
        // Arrange — player has "wins" but not "points"
        var player = MakePlayer(Guid.NewGuid(), ("wins", 1));

        // Act
        var result = PlayRecordOutcomeCalculator.TotalScore(player);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TotalScore_PlayerHasNoScores_ReturnsNull()
    {
        // Arrange
        var player = MakePlayer(Guid.NewGuid());

        // Act
        var result = PlayRecordOutcomeCalculator.TotalScore(player);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void TotalScore_PointsDimensionCaseInsensitive_ReturnsValue()
    {
        // Arrange — dimension stored as "Points" (mixed case)
        var player = MakePlayerWithRawDimension(Guid.NewGuid(), "Points", 99);

        // Act
        var result = PlayRecordOutcomeCalculator.TotalScore(player);

        // Assert
        result.Should().Be(99);
    }

    [Fact]
    public void TotalScore_PlayerHasBothWinsAndPoints_ReturnsPointsValue()
    {
        // Arrange
        var player = MakePlayer(Guid.NewGuid(), ("wins", 1), ("points", 42));

        // Act
        var result = PlayRecordOutcomeCalculator.TotalScore(player);

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void TotalScore_NullArgument_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => PlayRecordOutcomeCalculator.TotalScore(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Scenario: Competitive vs Non-competitive combinations

    [Fact]
    public void Scenario_CompetitiveGame_WinnerAndNonWinner()
    {
        // Arrange: player A wins (wins=1), player B does not (wins=0), both have points
        var playerA = MakePlayer(Guid.NewGuid(), ("wins", 1), ("points", 100));
        var playerB = MakePlayer(Guid.NewGuid(), ("wins", 0), ("points", 60));

        // Act
        var winnerIds = PlayRecordOutcomeCalculator.WinnerPlayerIds([playerA, playerB]);
        var outcomeType = PlayRecordOutcomeCalculator.OutcomeType([playerA, playerB]);
        var scoreA = PlayRecordOutcomeCalculator.TotalScore(playerA);
        var scoreB = PlayRecordOutcomeCalculator.TotalScore(playerB);

        // Assert
        winnerIds.Should().ContainSingle().Which.Should().Be(playerA.Id);
        outcomeType.Should().Be("competitive");
        scoreA.Should().Be(100);
        scoreB.Should().Be(60);
    }

    [Fact]
    public void Scenario_FreeFormGame_NullGameId_NoWinsDimension()
    {
        // Arrange: free-form/cooperative game — no "wins" dimension for any player
        var player1 = MakePlayer(Guid.NewGuid(), ("points", 30));
        var player2 = MakePlayer(Guid.NewGuid(), ("points", 45));

        // Act
        var winnerIds = PlayRecordOutcomeCalculator.WinnerPlayerIds([player1, player2]);
        var outcomeType = PlayRecordOutcomeCalculator.OutcomeType([player1, player2]);

        // Assert
        winnerIds.Should().BeEmpty();
        outcomeType.Should().Be("none");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a <see cref="RecordPlayerEntity"/> with named score dimensions.
    /// Each tuple is (dimension, value).
    /// </summary>
    private static RecordPlayerEntity MakePlayer(Guid id, params (string Dimension, int Value)[] scores)
    {
        var player = new RecordPlayerEntity
        {
            Id = id,
            PlayRecordId = Guid.NewGuid(),
            DisplayName = $"Player-{id:N}",
            Scores = scores.Select(s => new RecordScoreEntity
            {
                Id = Guid.NewGuid(),
                RecordPlayerId = id,
                Dimension = s.Dimension,
                Value = s.Value
            }).ToList()
        };
        return player;
    }

    /// <summary>
    /// Creates a player with a single score using a raw dimension string (for case-insensitivity tests).
    /// </summary>
    private static RecordPlayerEntity MakePlayerWithRawDimension(Guid id, string dimension, int value)
    {
        return new RecordPlayerEntity
        {
            Id = id,
            PlayRecordId = Guid.NewGuid(),
            DisplayName = $"Player-{id:N}",
            Scores = new List<RecordScoreEntity>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RecordPlayerId = id,
                    Dimension = dimension,
                    Value = value
                }
            }
        };
    }

    #endregion
}
