using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class ValueObjectTests
{
    #region ParticipantInfo Tests

    [Fact]
    public void ParticipantInfo_Create_WithValidParameters_ShouldSucceed()
    {
        // Act
        var info = ParticipantInfo.Create("Player 1", true, 1);

        // Assert
        info.Should().NotBeNull();
        info.DisplayName.Should().Be("Player 1");
        info.IsOwner.Should().BeTrue();
        info.JoinOrder.Should().Be(1);
    }

    [Fact]
    public void ParticipantInfo_ShouldBeImmutable()
    {
        // Arrange
        var info = ParticipantInfo.Create("Player 1", true, 1);

        // Assert
        info.Should().BeAssignableTo<ParticipantInfo>();
        // Record types are immutable by design
    }

    [Fact]
    public void ParticipantInfo_Create_WithEmptyDisplayName_ShouldThrow()
    {
        // Act
        var act = () => ParticipantInfo.Create("", true, 1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display name cannot be empty*");
    }

    [Fact]
    public void ParticipantInfo_Create_WithDisplayNameTooLong_ShouldThrow()
    {
        // Arrange
        var longName = new string('A', 51);

        // Act
        var act = () => ParticipantInfo.Create(longName, true, 1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display name cannot exceed 50 characters*");
    }

    [Fact]
    public void ParticipantInfo_Create_WithNonPositiveJoinOrder_ShouldThrow()
    {
        // Act
        var act = () => ParticipantInfo.Create("Player", true, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Join order must be positive*");
    }

    [Fact]
    public void ParticipantInfo_Create_ShouldTrimDisplayName()
    {
        // Act
        var info = ParticipantInfo.Create("  Player 1  ", false, 1);

        // Assert
        info.DisplayName.Should().Be("Player 1");
    }

    #endregion

    #region ScoreCalculation Tests

    [Fact]
    public void ScoreCalculation_Create_WithValidScores_ShouldCalculateCorrectly()
    {
        // Arrange
        var participantId = Guid.NewGuid();
        var scores = new[] { 10m, 20m, 30m };

        // Act
        var calculation = ScoreCalculation.Create(participantId, scores, rank: 2);

        // Assert
        calculation.ParticipantId.Should().Be(participantId);
        calculation.TotalScore.Should().Be(60m);
        calculation.AverageScore.Should().Be(20m);
        calculation.Rank.Should().Be(2);
        calculation.EntryCount.Should().Be(3);
    }

    [Fact]
    public void ScoreCalculation_CalculateTotal_ShouldSumScores()
    {
        // Arrange
        var scores = new[] { 5m, 10m, 15m };

        // Act
        var total = ScoreCalculation.CalculateTotal(scores);

        // Assert
        total.Should().Be(30m);
    }

    [Fact]
    public void ScoreCalculation_CalculateAverage_WithScores_ShouldReturnAverage()
    {
        // Arrange
        var scores = new[] { 10m, 20m, 30m };

        // Act
        var average = ScoreCalculation.CalculateAverage(scores);

        // Assert
        average.Should().Be(20m);
    }

    [Fact]
    public void ScoreCalculation_CalculateAverage_WithEmptyScores_ShouldReturnNull()
    {
        // Arrange
        var scores = Array.Empty<decimal>();

        // Act
        var average = ScoreCalculation.CalculateAverage(scores);

        // Assert
        average.Should().BeNull();
    }

    [Fact]
    public void ScoreCalculation_CalculateRanks_ShouldRankByTotalScore()
    {
        // Arrange
        var participant1 = Guid.NewGuid();
        var participant2 = Guid.NewGuid();
        var participant3 = Guid.NewGuid();
        var scores = new[]
        {
            (participant1, 100m),
            (participant2, 50m),
            (participant3, 75m)
        };

        // Act
        var ranks = ScoreCalculation.CalculateRanks(scores).ToList();

        // Assert
        ranks.Should().HaveCount(3);
        ranks.First(r => r.ParticipantId == participant1).Rank.Should().Be(1); // Highest
        ranks.First(r => r.ParticipantId == participant3).Rank.Should().Be(2);
        ranks.First(r => r.ParticipantId == participant2).Rank.Should().Be(3); // Lowest
    }

    #endregion

    #region SessionResult Tests

    [Fact]
    public void SessionResult_Create_WithValidData_ShouldSucceed()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var participant2Id = Guid.NewGuid();
        var finalRanks = new Dictionary<Guid, int>
        {
            { winnerId, 1 },
            { participant2Id, 2 }
        };
        var statistics = SessionStatistics.Create(5, 120, 10, 3);

        // Act
        var result = SessionResult.Create(winnerId, finalRanks, statistics);

        // Assert
        result.WinnerId.Should().Be(winnerId);
        result.FinalRanks.Should().HaveCount(2);
        result.Statistics.TotalRounds.Should().Be(5);
    }

    [Fact]
    public void SessionResult_Create_WithWinnerNotInRanks_ShouldThrow()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var otherParticipant = Guid.NewGuid();
        var finalRanks = new Dictionary<Guid, int> { { otherParticipant, 1 } };
        var statistics = SessionStatistics.Empty;

        // Act
        var act = () => SessionResult.Create(winnerId, finalRanks, statistics);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Winner ID must be in final ranks*");
    }

    [Fact]
    public void SessionResult_Create_WithWinnerNotRank1_ShouldThrow()
    {
        // Arrange
        var winnerId = Guid.NewGuid();
        var finalRanks = new Dictionary<Guid, int> { { winnerId, 2 } };
        var statistics = SessionStatistics.Empty;

        // Act
        var act = () => SessionResult.Create(winnerId, finalRanks, statistics);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Winner must have rank 1*");
    }

    [Fact]
    public void SessionResult_Create_WithNonSequentialRanks_ShouldThrow()
    {
        // Arrange
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var finalRanks = new Dictionary<Guid, int> { { p1, 1 }, { p2, 3 } }; // Skip rank 2
        var statistics = SessionStatistics.Empty;

        // Act
        var act = () => SessionResult.Create(p1, finalRanks, statistics);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Ranks must be sequential starting from 1*");
    }

    #endregion

    #region SessionStatistics Tests

    [Fact]
    public void SessionStatistics_Empty_ShouldHaveZeroValues()
    {
        // Act
        var statistics = SessionStatistics.Empty;

        // Assert
        statistics.TotalRounds.Should().Be(0);
        statistics.DurationMinutes.Should().Be(0);
        statistics.TotalScoreEntries.Should().Be(0);
        statistics.TotalNotes.Should().Be(0);
    }

    [Fact]
    public void SessionStatistics_Create_WithValidValues_ShouldSucceed()
    {
        // Act
        var statistics = SessionStatistics.Create(10, 180, 40, 5);

        // Assert
        statistics.TotalRounds.Should().Be(10);
        statistics.DurationMinutes.Should().Be(180);
        statistics.TotalScoreEntries.Should().Be(40);
        statistics.TotalNotes.Should().Be(5);
    }

    [Fact]
    public void SessionStatistics_Create_WithNegativeRounds_ShouldThrow()
    {
        // Act
        var act = () => SessionStatistics.Create(-1, 60, 10, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Total rounds cannot be negative*");
    }

    [Fact]
    public void SessionStatistics_Create_WithNegativeDuration_ShouldThrow()
    {
        // Act
        var act = () => SessionStatistics.Create(5, -10, 10, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Duration cannot be negative*");
    }

    #endregion
}
