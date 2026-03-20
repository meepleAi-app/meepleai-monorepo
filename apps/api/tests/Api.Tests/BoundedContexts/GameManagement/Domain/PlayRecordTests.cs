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
        Assert.NotNull(record.ScoringConfig);
        record.DomainEvents.Should().ContainSingle();
        Assert.IsType<PlayRecordCreatedEvent>(record.DomainEvents.First());
    }

    [Fact]
    public void CreateWithGame_EmptyGameId_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PlayRecord.CreateWithGame(
                Guid.NewGuid(),
                Guid.Empty,
                "Catan",
                Guid.NewGuid(),
                DateTime.UtcNow,
                PlayRecordVisibility.Private));

        Assert.Contains("GameId cannot be empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateWithGame_EmptyGameName_ThrowsValidationException()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            PlayRecord.CreateWithGame(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "",
                Guid.NewGuid(),
                DateTime.UtcNow,
                PlayRecordVisibility.Private));
    }

    [Fact]
    public void CreateWithGame_FutureSessionDate_ThrowsValidationException()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PlayRecord.CreateWithGame(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Catan",
                Guid.NewGuid(),
                futureDate,
                PlayRecordVisibility.Private));

        Assert.Contains("cannot be in the future", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateWithGame_GroupVisibilityWithoutGroupId_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PlayRecord.CreateWithGame(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Catan",
                Guid.NewGuid(),
                DateTime.UtcNow,
                PlayRecordVisibility.Group,
                groupId: null));

        Assert.Contains("GroupId is required", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.Null(record.GameId);  // No catalog game
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
        Assert.Throws<ArgumentNullException>(() =>
            PlayRecord.CreateFreeForm(
                Guid.NewGuid(),
                "Poker",
                Guid.NewGuid(),
                DateTime.UtcNow,
                PlayRecordVisibility.Private,
                null!));
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
        Assert.True(player.IsRegisteredUser);
        Assert.False(player.IsGuest);
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
        Assert.Null(player.UserId);
        player.DisplayName.Should().Be("Bob");
        Assert.False(player.IsRegisteredUser);
        Assert.True(player.IsGuest);
    }

    [Fact]
    public void AddPlayer_DuplicateUserId_ThrowsDomainException()
    {
        // Arrange
        var record = CreateTestRecord();
        var userId = Guid.NewGuid();
        record.AddPlayer(userId, "Alice");

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            record.AddPlayer(userId, "Alice Again"));

        Assert.Contains("already a player", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddPlayer_DuplicateDisplayName_ThrowsDomainException()
    {
        // Arrange
        var record = CreateTestRecord();
        record.AddPlayer(Guid.NewGuid(), "Alice");

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            record.AddPlayer(Guid.NewGuid(), "Alice"));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddPlayer_EmptyDisplayName_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            record.AddPlayer(Guid.NewGuid(), ""));
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
        var exception = Assert.Throws<DomainException>(() =>
            record.AddPlayer(null, "Player101"));

        Assert.Contains("Cannot add more than 100 players", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.NotNull(player);
        player.Scores.Should().ContainSingle();
        var recordedScore = player.GetScore("points");
        Assert.NotNull(recordedScore);
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
        Assert.NotNull(player);
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
        Assert.Single(player!.Scores);  // Only one score per dimension
        player.GetScore("points")!.Value.Should().Be(20);
    }

    [Fact]
    public void RecordScore_InvalidPlayerId_ThrowsDomainException()
    {
        // Arrange
        var record = CreateTestRecord();
        var invalidPlayerId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            record.RecordScore(invalidPlayerId, RecordScore.Points(42)));

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
        var exception = Assert.Throws<ValidationException>(() =>
            record.RecordScore(playerId, RecordScore.Ranking(1)));

        Assert.Contains("not enabled", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.NotNull(record.StartTime);
        Assert.True(record.DomainEvents.Any(e => e is PlayRecordStartedEvent));
    }

    [Fact]
    public void Start_NonPlannedRecord_ThrowsConflictException()
    {
        // Arrange
        var record = CreateTestRecord();
        record.Start();

        // Act & Assert
        var exception = Assert.Throws<ConflictException>(() =>
            record.Start());

        Assert.Contains("Must be Planned", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.NotNull(record.EndTime);
        record.Duration.Should().Be(manualDuration);
        Assert.True(record.DomainEvents.Any(e => e is PlayRecordCompletedEvent));
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
        Assert.NotNull(record.Duration);
        Assert.True(record.Duration > TimeSpan.Zero);
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
        var exception = Assert.Throws<ValidationException>(() =>
            record.Complete(TimeSpan.FromHours(-1)));

        Assert.Contains("cannot be negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Complete_DurationOver30Days_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            record.Complete(TimeSpan.FromDays(31)));

        Assert.Contains("cannot exceed 30 days", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Complete_AlreadyCompleted_ThrowsConflictException()
    {
        // Arrange
        var record = CreateTestRecord();
        record.Complete();

        // Act & Assert
        var exception = Assert.Throws<ConflictException>(() =>
            record.Complete());

        Assert.Contains("already completed", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        Assert.True(record.DomainEvents.Any(e => e is PlayRecordUpdatedEvent));
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
        var exception = Assert.Throws<ValidationException>(() =>
            record.UpdateDetails(sessionDate: futureDate));

        Assert.Contains("cannot be in the future", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateDetails_NotesTooLong_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();
        var longNotes = new string('x', 2001);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            record.UpdateDetails(notes: longNotes));

        Assert.Contains("cannot exceed 2000", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateDetails_LocationTooLong_ThrowsValidationException()
    {
        // Arrange
        var record = CreateTestRecord();
        var longLocation = new string('x', 256);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            record.UpdateDetails(location: longLocation));

        Assert.Contains("cannot exceed 255", exception.Message, StringComparison.OrdinalIgnoreCase);
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
        var exception = Assert.Throws<ConflictException>(() =>
            record.Archive());

        Assert.Contains("Only completed records", exception.Message, StringComparison.OrdinalIgnoreCase);
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
