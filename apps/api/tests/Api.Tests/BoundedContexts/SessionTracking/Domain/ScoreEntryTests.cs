using Api.BoundedContexts.SessionTracking.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class ScoreEntryTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var scoreEntry = ScoreEntry.Create(sessionId, participantId, 42.5m, createdBy, roundNumber: 1);

        // Assert
        scoreEntry.Should().NotBeNull();
        scoreEntry.Id.Should().NotBeEmpty();
        scoreEntry.SessionId.Should().Be(sessionId);
        scoreEntry.ParticipantId.Should().Be(participantId);
        scoreEntry.ScoreValue.Should().Be(42.5m);
        scoreEntry.RoundNumber.Should().Be(1);
        scoreEntry.CreatedBy.Should().Be(createdBy);
    }

    [Fact]
    public void Create_WithDecimalScore_ShouldPreservePrecision()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var preciseScore = 123.45m;

        // Act
        var scoreEntry = ScoreEntry.Create(sessionId, participantId, preciseScore, createdBy, roundNumber: 1);

        // Assert
        scoreEntry.ScoreValue.Should().Be(123.45m);
    }

    [Fact]
    public void Create_WithRoundNumberOnly_ShouldSucceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var scoreEntry = ScoreEntry.Create(sessionId, participantId, 10m, createdBy, roundNumber: 2);

        // Assert
        scoreEntry.RoundNumber.Should().Be(2);
        scoreEntry.Category.Should().BeNull();
    }

    [Fact]
    public void Create_WithCategoryOnly_ShouldSucceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var scoreEntry = ScoreEntry.Create(sessionId, participantId, 50m, createdBy, category: "Military");

        // Assert
        scoreEntry.Category.Should().Be("Military");
        scoreEntry.RoundNumber.Should().BeNull();
    }

    [Fact]
    public void Create_WithBothRoundAndCategory_ShouldSucceed()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var scoreEntry = ScoreEntry.Create(sessionId, participantId, 25m, createdBy, roundNumber: 3, category: "Science");

        // Assert
        scoreEntry.RoundNumber.Should().Be(3);
        scoreEntry.Category.Should().Be("Science");
    }

    [Fact]
    public void Create_WithNeitherRoundNorCategory_ShouldThrow()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var act = () => ScoreEntry.Create(sessionId, participantId, 10m, createdBy);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Either round number or category must be provided*");
    }

    [Fact]
    public void Create_WithEmptySessionId_ShouldThrow()
    {
        // Act
        var act = () => ScoreEntry.Create(Guid.Empty, Guid.NewGuid(), 10m, Guid.NewGuid(), roundNumber: 1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Session ID cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyParticipantId_ShouldThrow()
    {
        // Act
        var act = () => ScoreEntry.Create(Guid.NewGuid(), Guid.Empty, 10m, Guid.NewGuid(), roundNumber: 1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Participant ID cannot be empty*");
    }

    [Fact]
    public void Create_WithNegativeRoundNumber_ShouldThrow()
    {
        // Act
        var act = () => ScoreEntry.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, Guid.NewGuid(), roundNumber: -1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Round number must be positive*");
    }

    [Fact]
    public void Create_WithCategoryTooLong_ShouldThrow()
    {
        // Arrange
        var longCategory = new string('A', 51);

        // Act
        var act = () => ScoreEntry.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, Guid.NewGuid(), category: longCategory);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Category cannot exceed 50 characters*");
    }

    [Fact]
    public void UpdateScore_ShouldChangeValueAndTimestamp()
    {
        // Arrange
        var scoreEntry = ScoreEntry.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, Guid.NewGuid(), roundNumber: 1);
        var originalTimestamp = scoreEntry.Timestamp;

        // Act
        Thread.Sleep(10); // Ensure timestamp changes
        scoreEntry.UpdateScore(20m);

        // Assert
        scoreEntry.ScoreValue.Should().Be(20m);
        scoreEntry.Timestamp.Should().BeAfter(originalTimestamp);
    }
}
