using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]
public class PlayRecordTests
{
    #region Factory Method Tests

    [Fact]
    public void CreateWithGame_ValidParameters_CreatesSuccessfully()
    {
        // Arrange
        var id = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionDate = DateTime.UtcNow.AddDays(-1);

        // Act
        var record = PlayRecord.CreateWithGame(
            id,
            gameId,
            "Catan",
            userId,
            sessionDate,
            PlayRecordVisibility.Private);

        // Assert
        record.Id.Should().Be(id);
        record.GameId.Should().Be(gameId);
        record.GameName.Should().Be("Catan");
        record.CreatedByUserId.Should().Be(userId);
        record.SessionDate.Should().Be(sessionDate);
        record.Visibility.Should().Be(PlayRecordVisibility.Private);
        record.Status.Should().Be(PlayRecordStatus.Planned);
        record.ScoringConfig.Should().NotBeNull();
        record.DomainEvents.Should().ContainSingle();
        record.DomainEvents.First().Should().BeOfType<PlayRecordCreatedEvent>();
    }

    [Fact]
    public void CreateWithGame_EmptyGameId_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () =>
            PlayRecord.CreateWithGame(
                Guid.NewGuid(),
                Guid.Empty,
                "Catan",
                Guid.NewGuid(),
                DateTime.UtcNow,
                PlayRecordVisibility.Private);
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.Message.Should().ContainEquivalentOf("GameId cannot be empty");
    }

    [Fact]
    public void CreateWithGame_EmptyGameName_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            PlayRecord.CreateWithGame(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "",
                Guid.NewGuid(),
                DateTime.UtcNow,
                PlayRecordVisibility.Private);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void CreateWithGame_FutureSessionDate_ThrowsValidationException()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);

        // Act & Assert
        var act = () =>
            PlayRecord.CreateWithGame(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Catan",
                Guid.NewGuid(),
                futureDate,
                PlayRecordVisibility.Private);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("cannot be in the future");
    }

    [Fact]
    public void CreateWithGame_GroupVisibilityWithoutGroupId_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            PlayRecord.CreateWithGame(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Catan",
                Guid.NewGuid(),
                DateTime.UtcNow,
                PlayRecordVisibility.Group,
                groupId: null);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("GroupId is required");
    }

    [Fact]
    public void CreateFreeForm_ValidParameters_CreatesSuccessfully()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionDate = DateTime.UtcNow.AddHours(-2);
        var scoringConfig = SessionScoringConfig.CreateDefault();

        // Act
        var record = PlayRecord.CreateFreeForm(
            id,
            "Poker",
            userId,
            sessionDate,
            PlayRecordVisibility.Private,
            scoringConfig);

        // Assert
        record.Id.Should().Be(id);
        record.GameId.Should().BeNull();  // No catalog game
        record.GameName.Should().Be("Poker");
        record.CreatedByUserId.Should().Be(userId);
        record.SessionDate.Should().Be(sessionDate);
        record.ScoringConfig.Should().Be(scoringConfig);
        record.Status.Should().Be(PlayRecordStatus.Planned);
    }

    [Fact]
    public void CreateFreeForm_NullScoringConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            PlayRecord.CreateFreeForm(
                Guid.NewGuid(),
                "Poker",
                Guid.NewGuid(),
                DateTime.UtcNow,
                PlayRecordVisibility.Private,
                null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region AddPlayer Tests

    [Fact]
    public void AddPlayer_RegisteredUser_AddsSuccessfully()
    {
        // Arrange
        var record = CreateTestRecord();
        var userId = Guid.NewGuid();

        // Act
        record.AddPlayer(userId, "Alice");

        // Assert
        record.Players.Should().ContainSingle();
        var player = record.Players.First();
        player.UserId.Should().Be(userId);
        player.DisplayName.Should().Be("Alice");
        (player.IsRegisteredUser).Should().BeTrue();
        (player.IsGuest).Should().BeFalse();
    }

    [Fact]
    public void AddPlayer_Guest_AddsSuccessfully()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act
        record.AddPlayer(null, "Bob");

        // Assert
        record.Players.Should().ContainSingle();
        var player = record.Players.First();
        player.UserId.Should().BeNull();
        player.DisplayName.Should().Be("Bob");
        (player.IsRegisteredUser).Should().BeFalse();
        (player.IsGuest).Should().BeTrue();
    }

    [Fact]
    public void AddPlayer_DuplicateUserId_ThrowsDomainException()
    {
        // Arrange
        var record = CreateTestRecord();
        var userId = Guid.NewGuid();
        record.AddPlayer(userId, "Alice");

        // Act & Assert
        var act = () =>
            record.AddPlayer(userId, "Alice Again");
        var exception = act.Should().Throw<DomainException>().Which;

        exception.Message.Should().ContainEquivalentOf("already a player");
    }

    [Fact]
    public void AddPlayer_DuplicateDisplayName_ThrowsDomainException()
    {
        // Arrange
        var record = CreateTestRecord();
        record.AddPlayer(Guid.NewGuid(), "Alice");

        // Act & Assert
        var act = () =>
            record.AddPlayer(Guid.NewGuid(), "Alice");
        var exception = act.Should().Throw<DomainException>().Which;

        exception.Message.Should().ContainEquivalentOf("already exists");
    }

    [Fact]
    public void AddPlayer_EmptyDisplayName_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act & Assert
        var act = () =>
            record.AddPlayer(Guid.NewGuid(), "");
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void AddPlayer_MoreThan100Players_ThrowsDomainException()
    {
        // Arrange
        var record = CreateTestRecord();
        for (int i = 0; i < 100; i++)
        {
            record.AddPlayer(null, $"Player{i}");
        }

        // Act & Assert
        var act = () =>
            record.AddPlayer(null, "Player101");
        var exception = act.Should().Throw<DomainException>().Which;

        exception.Message.Should().ContainEquivalentOf("Cannot add more than 100 players");
    }

    #endregion

    #region RecordScore Tests

    [Fact]
    public void RecordScore_ValidScore_RecordsSuccessfully()
    {
        // Arrange
        var record = CreateTestRecord();
        record.AddPlayer(Guid.NewGuid(), "Alice");
        var playerId = record.Players.First().Id;
        var score = RecordScore.Points(42);

        // Act
        record.RecordScore(playerId, score);

        // Assert
        var player = record.GetPlayer(playerId);
        player.Should().NotBeNull();
        player.Scores.Should().ContainSingle();
        var recordedScore = player.GetScore("points");
        recordedScore.Should().NotBeNull();
        recordedScore.Value.Should().Be(42);
    }

    [Fact]
    public void RecordScore_MultipleScores_RecordsBothSuccessfully()
    {
        // Arrange
        var scoringConfig = new SessionScoringConfig(
            new List<string> { "points", "ranking" },
            new Dictionary<string, string> { ["points"] = "pts", ["ranking"] = "#" });

        var record = PlayRecord.CreateFreeForm(
            Guid.NewGuid(),
            "Test Game",
            Guid.NewGuid(),
            DateTime.UtcNow,
            PlayRecordVisibility.Private,
            scoringConfig);

        record.AddPlayer(Guid.NewGuid(), "Alice");
        var playerId = record.Players.First().Id;

        // Act
        record.RecordScore(playerId, RecordScore.Points(42));
        record.RecordScore(playerId, RecordScore.Ranking(1));

        // Assert
        var player = record.GetPlayer(playerId);
        player.Should().NotBeNull();
        player.Scores.Count.Should().Be(2);
        player.GetScore("points")!.Value.Should().Be(42);
        player.GetScore("ranking")!.Value.Should().Be(1);
    }

    [Fact]
    public void RecordScore_ReplacesExistingScore()
    {
        // Arrange
        var record = CreateTestRecord();
        record.AddPlayer(Guid.NewGuid(), "Alice");
        var playerId = record.Players.First().Id;

        record.RecordScore(playerId, RecordScore.Points(10));

        // Act - Record new score for same dimension
        record.RecordScore(playerId, RecordScore.Points(20));

        // Assert
        var player = record.GetPlayer(playerId);
        player!.Scores.Should().ContainSingle();  // Only one score per dimension
        player.GetScore("points")!.Value.Should().Be(20);
    }

    [Fact]
    public void RecordScore_InvalidPlayerId_ThrowsDomainException()
    {
        // Arrange
        var record = CreateTestRecord();
        var invalidPlayerId = Guid.NewGuid();

        // Act & Assert
        var act = () =>
            record.RecordScore(invalidPlayerId, RecordScore.Points(42));
        var exception = act.Should().Throw<DomainException>().Which;

        exception.Message.Should().Contain("Player");
        exception.Message.Should().Contain("not found");
    }

    [Fact]
    public void RecordScore_DimensionNotInConfig_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();  // Has only "points" dimension
        record.AddPlayer(Guid.NewGuid(), "Alice");
        var playerId = record.Players.First().Id;

        // Act & Assert
        var act = () =>
            record.RecordScore(playerId, RecordScore.Ranking(1));
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("not enabled");
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public void Start_PlannedRecord_StartsSuccessfully()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act
        record.Start();

        // Assert
        record.Status.Should().Be(PlayRecordStatus.InProgress);
        record.StartTime.Should().NotBeNull();
        (record.DomainEvents.Any(e => e is PlayRecordStartedEvent)).Should().BeTrue();
    }

    [Fact]
    public void Start_NonPlannedRecord_ThrowsConflictException()
    {
        // Arrange
        var record = CreateTestRecord();
        record.Start();

        // Act & Assert
        var act = () =>
            record.Start();
        var exception = act.Should().Throw<ConflictException>().Which;

        exception.Message.Should().ContainEquivalentOf("Must be Planned");
    }

    [Fact]
    public void Complete_WithManualDuration_CompletesSuccessfully()
    {
        // Arrange
        var record = CreateTestRecord();
        var manualDuration = TimeSpan.FromHours(2);

        // Act
        record.Complete(manualDuration);

        // Assert
        record.Status.Should().Be(PlayRecordStatus.Completed);
        record.EndTime.Should().NotBeNull();
        record.Duration.Should().Be(manualDuration);
        (record.DomainEvents.Any(e => e is PlayRecordCompletedEvent)).Should().BeTrue();
    }

    [Fact]
    public void Complete_WithStartTime_CalculatesDuration()
    {
        // Arrange
        var record = CreateTestRecord();
        record.Start();
        Thread.Sleep(100);  // Small delay

        // Act
        record.Complete();

        // Assert
        record.Status.Should().Be(PlayRecordStatus.Completed);
        record.Duration.Should().NotBeNull();
        (record.Duration > TimeSpan.Zero).Should().BeTrue();
    }

    [Fact]
    public void Complete_WithoutStartTime_UsesDurationZero()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act
        record.Complete();

        // Assert
        record.Status.Should().Be(PlayRecordStatus.Completed);
        record.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Complete_NegativeDuration_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act & Assert
        var act = () =>
            record.Complete(TimeSpan.FromHours(-1));
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("cannot be negative");
    }

    [Fact]
    public void Complete_DurationOver30Days_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act & Assert
        var act = () =>
            record.Complete(TimeSpan.FromDays(31));
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("cannot exceed 30 days");
    }

    [Fact]
    public void Complete_AlreadyCompleted_ThrowsConflictException()
    {
        // Arrange
        var record = CreateTestRecord();
        record.Complete();

        // Act & Assert
        var act = () =>
            record.Complete();
        var exception = act.Should().Throw<ConflictException>().Which;

        exception.Message.Should().ContainEquivalentOf("already completed");
    }

    #endregion

    #region UpdateDetails Tests

    [Fact]
    public void UpdateDetails_ValidData_UpdatesSuccessfully()
    {
        // Arrange
        var record = CreateTestRecord();
        var newDate = DateTime.UtcNow.AddDays(-2);

        // Act
        record.UpdateDetails(
            sessionDate: newDate,
            notes: "Great game!",
            location: "Home");

        // Assert
        record.SessionDate.Should().Be(newDate);
        record.Notes.Should().Be("Great game!");
        record.Location.Should().Be("Home");
        (record.DomainEvents.Any(e => e is PlayRecordUpdatedEvent)).Should().BeTrue();
    }

    [Fact]
    public void UpdateDetails_AfterCompletion_AllowsUpdates()
    {
        // Arrange
        var record = CreateTestRecord();
        record.Complete();

        // Act - Should not throw
        record.UpdateDetails(notes: "Corrected notes");

        // Assert
        record.Notes.Should().Be("Corrected notes");
    }

    [Fact]
    public void UpdateDetails_FutureSessionDate_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();
        var futureDate = DateTime.UtcNow.AddDays(1);

        // Act & Assert
        var act = () =>
            record.UpdateDetails(sessionDate: futureDate);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("cannot be in the future");
    }

    [Fact]
    public void UpdateDetails_NotesTooLong_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();
        var longNotes = new string('x', 2001);

        // Act & Assert
        var act = () =>
            record.UpdateDetails(notes: longNotes);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("cannot exceed 2000");
    }

    [Fact]
    public void UpdateDetails_LocationTooLong_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();
        var longLocation = new string('x', 256);

        // Act & Assert
        var act = () =>
            record.UpdateDetails(location: longLocation);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("cannot exceed 255");
    }

    #endregion

    #region Archive Tests

    [Fact]
    public void Archive_CompletedRecord_ArchivesSuccessfully()
    {
        // Arrange
        var record = CreateTestRecord();
        record.Complete();

        // Act
        record.Archive();

        // Assert
        record.Status.Should().Be(PlayRecordStatus.Archived);
    }

    [Fact]
    public void Archive_NonCompletedRecord_ThrowsConflictException()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act & Assert
        var act = () =>
            record.Archive();
        var exception = act.Should().Throw<ConflictException>().Which;

        exception.Message.Should().ContainEquivalentOf("Only completed records");
    }

    #endregion

    #region Helper Methods

    private static PlayRecord CreateTestRecord()
    {
        return PlayRecord.CreateWithGame(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Game",
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(-1),
            PlayRecordVisibility.Private);
    }

    #endregion
}
