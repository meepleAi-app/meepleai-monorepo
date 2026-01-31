using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class SessionTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var session = Session.Create(userId, gameId, SessionType.GameSpecific);

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().NotBeEmpty();
        session.UserId.Should().Be(userId);
        session.GameId.Should().Be(gameId);
        session.SessionType.Should().Be(SessionType.GameSpecific);
        session.Status.Should().Be(SessionStatus.Active);
        session.IsDeleted.Should().BeFalse();
        session.CreatedBy.Should().Be(userId);
    }

    [Fact]
    public void Create_ShouldGenerateUniqueSessionCode()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var session = Session.Create(userId, null, SessionType.Generic);

        // Assert
        session.SessionCode.Should().HaveLength(6);
        session.SessionCode.Should().MatchRegex("^[A-Z2-9]{6}$");
        session.SessionCode.Should().NotContain("0");
        session.SessionCode.Should().NotContain("O");
        session.SessionCode.Should().NotContain("I");
        session.SessionCode.Should().NotContain("1");
    }

    [Fact]
    public void Create_ShouldAutomaticallyAddOwnerAsFirstParticipant()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var session = Session.Create(userId, null, SessionType.Generic);

        // Assert
        session.Participants.Should().HaveCount(1);
        var owner = session.Participants.First();
        owner.IsOwner.Should().BeTrue();
        owner.JoinOrder.Should().Be(1);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrow()
    {
        // Act
        var act = () => Session.Create(Guid.Empty, null, SessionType.Generic);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*User ID cannot be empty*");
    }

    [Fact]
    public void Create_GameSpecificWithoutGameId_ShouldThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var act = () => Session.Create(userId, null, SessionType.GameSpecific);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Game ID required for GameSpecific sessions*");
    }

    [Fact]
    public void AddParticipant_WithValidInfo_ShouldSucceed()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);
        var participantInfo = ParticipantInfo.Create("Player 2", false, 2);

        // Act
        session.AddParticipant(participantInfo);

        // Assert
        session.Participants.Should().HaveCount(2);
        var newParticipant = session.Participants.Last();
        newParticipant.DisplayName.Should().Be("Player 2");
        newParticipant.IsOwner.Should().BeFalse();
        newParticipant.JoinOrder.Should().Be(2);
    }

    [Fact]
    public void AddParticipant_WhenFinalized_ShouldThrow()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);
        session.Finalize();
        var participantInfo = ParticipantInfo.Create("Player 2", false, 2);

        // Act
        var act = () => session.AddParticipant(participantInfo);

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("*Cannot add participants to finalized session*");
    }

    [Fact]
    public void AddParticipant_WithNullInfo_ShouldThrow()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);

        // Act
        var act = () => session.AddParticipant(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Pause_WhenActive_ShouldUpdateStatus()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);

        // Act
        session.Pause();

        // Assert
        session.Status.Should().Be(SessionStatus.Paused);
        session.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Pause_WhenNotActive_ShouldThrow()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);
        session.Pause(); // First pause

        // Act
        var act = () => session.Pause(); // Try to pause again

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("*Cannot pause session with status*");
    }

    [Fact]
    public void Resume_WhenPaused_ShouldUpdateStatus()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);
        session.Pause();

        // Act
        session.Resume();

        // Assert
        session.Status.Should().Be(SessionStatus.Active);
        session.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Resume_WhenNotPaused_ShouldThrow()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);

        // Act
        var act = () => session.Resume();

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("*Cannot resume session with status*");
    }

    [Fact]
    public void Finalize_WhenActive_ShouldUpdateStatus()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);

        // Act
        session.Finalize();

        // Assert
        session.Status.Should().Be(SessionStatus.Finalized);
        session.FinalizedAt.Should().NotBeNull();
        session.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Finalize_WithWinnerId_ShouldSetWinnerRank()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);
        var participantInfo = ParticipantInfo.Create("Player 2", false, 2);
        session.AddParticipant(participantInfo);
        var winnerId = session.Participants.Last().Id;

        // Act
        session.Finalize(winnerId);

        // Assert
        var winner = session.Participants.First(p => p.Id == winnerId);
        winner.FinalRank.Should().Be(1);
    }

    [Fact]
    public void Finalize_WithInvalidWinnerId_ShouldThrow()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);
        var invalidWinnerId = Guid.NewGuid();

        // Act
        var act = () => session.Finalize(invalidWinnerId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Winner ID must be a valid participant*");
    }

    [Fact]
    public void Finalize_WhenAlreadyFinalized_ShouldThrow()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);
        session.Finalize();

        // Act
        var act = () => session.Finalize();

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("*Session is already finalized*");
    }

    [Fact]
    public void SoftDelete_ShouldSetDeletedFlags()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);

        // Act
        session.SoftDelete();

        // Assert
        session.IsDeleted.Should().BeTrue();
        session.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateAudit_ShouldSetTimestampAndUser()
    {
        // Arrange
        var session = Session.Create(Guid.NewGuid(), null, SessionType.Generic);
        var updaterId = Guid.NewGuid();

        // Act
        session.UpdateAudit(updaterId);

        // Assert
        session.UpdatedAt.Should().NotBeNull();
        session.UpdatedBy.Should().Be(updaterId);
    }
}
