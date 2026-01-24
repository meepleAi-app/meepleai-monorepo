using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Domain.Entities;

public sealed class UserLibraryEntryTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesWithDefaultState()
    {
        // Act
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Assert
        entry.CurrentState.Should().NotBeNull();
        entry.CurrentState.Value.Should().Be(GameStateType.Nuovo, "new games default to Nuovo state");
        entry.Stats.Should().NotBeNull();
        entry.Stats.TimesPlayed.Should().Be(0, "new games have no play history");
        entry.Sessions.Should().BeEmpty();
        entry.Checklist.Should().BeEmpty();
    }

    #endregion

    #region State Management Tests

    [Fact]
    public void ChangeState_ToOwned_UpdatesStateAndRaisesDomainEvent()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        entry.ChangeState(GameState.Owned("Disponibile"));

        // Assert
        entry.CurrentState.Value.Should().Be(GameStateType.Owned);
        entry.CurrentState.StateNotes.Should().Be("Disponibile");

        var domainEvent = entry.DomainEvents.OfType<GameStateChangedEvent>().FirstOrDefault();
        domainEvent.Should().NotBeNull();
        domainEvent!.PreviousState.Should().Be(GameStateType.Nuovo);
        domainEvent.NewState.Should().Be(GameStateType.Owned);
        domainEvent.UserId.Should().Be(_userId);
        domainEvent.GameId.Should().Be(_gameId);
    }

    [Fact]
    public void ChangeState_InvalidTransition_ThrowsConflictException()
    {
        // Arrange - Start with Wishlist
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        entry.ChangeState(GameState.Wishlist());

        // Act - Try to loan a wishlist item (invalid)
        Action act = () => entry.ChangeState(GameState.InPrestito("Mario"));

        // Assert
        act.Should().Throw<ConflictException>()
            .WithMessage("*Wishlist*InPrestito*");
    }

    [Fact]
    public void MarkAsOwned_ChangesStateToOwned()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        entry.MarkAsOwned("Game available");

        // Assert
        entry.CurrentState.Value.Should().Be(GameStateType.Owned);
    }

    [Fact]
    public void MarkAsOnLoan_ChangesStateToInPrestito()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        entry.MarkAsOwned(); // Must be owned first

        // Act
        entry.MarkAsOnLoan("Borrowed by Mario");

        // Assert
        entry.CurrentState.Value.Should().Be(GameStateType.InPrestito);
        entry.CurrentState.StateNotes.Should().Be("Borrowed by Mario");
    }

    [Fact]
    public void AddToWishlist_ChangesStateToWishlist()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        entry.AddToWishlist("Want to buy");

        // Assert
        entry.CurrentState.Value.Should().Be(GameStateType.Wishlist);
    }

    [Fact]
    public void IsAvailableForPlay_WhenOwned_ReturnsTrue()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        entry.MarkAsOwned();

        // Act
        var isAvailable = entry.IsAvailableForPlay();

        // Assert
        isAvailable.Should().BeTrue();
    }

    [Fact]
    public void IsAvailableForPlay_WhenOnLoan_ReturnsFalse()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        entry.MarkAsOwned();
        entry.MarkAsOnLoan("Borrowed");

        // Act
        var isAvailable = entry.IsAvailableForPlay();

        // Assert
        isAvailable.Should().BeFalse();
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public void RecordGameSession_AddsSessionAndUpdatesStats()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        var playedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var session = entry.RecordGameSession(
            playedAt: playedAt,
            durationMinutes: 60,
            didWin: true,
            players: "Alice,Bob,Charlie",
            notes: "Great game!");

        // Assert
        session.Should().NotBeNull();
        entry.Sessions.Should().ContainSingle();
        entry.Sessions.First().Should().Be(session);

        entry.Stats.TimesPlayed.Should().Be(1);
        entry.Stats.LastPlayed.Should().Be(playedAt);
        entry.Stats.AvgDuration.Should().Be(60);
        entry.Stats.WinRate.Should().Be(100m);
    }

    [Fact]
    public void RecordGameSession_RaisesDomainEvent()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        var session = entry.RecordGameSession(
            playedAt: DateTime.UtcNow,
            durationMinutes: 90);

        // Assert
        var domainEvent = entry.DomainEvents.OfType<GameSessionRecordedEvent>().FirstOrDefault();
        domainEvent.Should().NotBeNull();
        domainEvent!.SessionId.Should().Be(session.Id);
        domainEvent.DurationMinutes.Should().Be(90);
        domainEvent.UserId.Should().Be(_userId);
        domainEvent.GameId.Should().Be(_gameId);
    }

    [Fact]
    public void RecordGameSession_MultipleSession_UpdatesStatsCorrectly()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act - Record 3 sessions
        entry.RecordGameSession(DateTime.UtcNow.AddDays(-3), 60, true);
        entry.RecordGameSession(DateTime.UtcNow.AddDays(-2), 90, false);
        entry.RecordGameSession(DateTime.UtcNow.AddDays(-1), 120, true);

        // Assert
        entry.Sessions.Count.Should().Be(3);
        entry.Stats.TimesPlayed.Should().Be(3);
        entry.Stats.AvgDuration.Should().Be(90, "(60 + 90 + 120) / 3 = 90");
        entry.Stats.WinRate.Should().BeApproximately(66.67m, 0.1m, "2 wins out of 3 = 66.67%");
    }

    [Fact]
    public void RemoveGameSession_RecalculatesStats()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        var session1 = entry.RecordGameSession(DateTime.UtcNow.AddDays(-2), 60, true);
        var session2 = entry.RecordGameSession(DateTime.UtcNow.AddDays(-1), 120, false);

        // Act
        entry.RemoveGameSession(session1.Id);

        // Assert
        entry.Sessions.Should().ContainSingle();
        entry.Sessions.First().Should().Be(session2);
        entry.Stats.TimesPlayed.Should().Be(1);
        entry.Stats.AvgDuration.Should().Be(120);
        entry.Stats.WinRate.Should().Be(0m, "only remaining session is a loss");
    }

    [Fact]
    public void RemoveGameSession_NonExistentSession_ThrowsNotFoundException()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        Action act = () => entry.RemoveGameSession(Guid.NewGuid());

        // Assert
        act.Should().Throw<NotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void GetLatestSession_WithSessions_ReturnsNewest()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        entry.RecordGameSession(DateTime.UtcNow.AddDays(-3), 60);
        var latest = entry.RecordGameSession(DateTime.UtcNow.AddDays(-1), 90);
        entry.RecordGameSession(DateTime.UtcNow.AddDays(-2), 120);

        // Act
        var result = entry.GetLatestSession();

        // Assert
        result.Should().Be(latest);
    }

    [Fact]
    public void GetLatestSession_NoSessions_ReturnsNull()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        var result = entry.GetLatestSession();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Checklist Management Tests

    [Fact]
    public void AddChecklistItem_AddsNewItem()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        var item = entry.AddChecklistItem("Shuffle deck", "Remove jokers first");

        // Assert
        item.Should().NotBeNull();
        entry.Checklist.Should().ContainSingle();
        entry.Checklist.First().Should().Be(item);
        item.Description.Should().Be("Shuffle deck");
        item.AdditionalInfo.Should().Be("Remove jokers first");
        item.Order.Should().Be(0, "first item has order 0");
    }

    [Fact]
    public void AddChecklistItem_MultipleItems_AssignsCorrectOrders()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        var item3 = entry.AddChecklistItem("Step 3");

        // Assert
        entry.Checklist.Count.Should().Be(3);
        item1.Order.Should().Be(0);
        item2.Order.Should().Be(1);
        item3.Order.Should().Be(2);
    }

    [Fact]
    public void RemoveChecklistItem_RemovesItemAndReordersRemaining()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        var item3 = entry.AddChecklistItem("Step 3");

        // Act - Remove middle item
        entry.RemoveChecklistItem(item2.Id);

        // Assert
        entry.Checklist.Count.Should().Be(2);
        entry.Checklist.Should().Contain(item1);
        entry.Checklist.Should().Contain(item3);
        entry.Checklist.Should().NotContain(item2);

        // Verify reordering
        var ordered = entry.GetOrderedChecklist();
        ordered[0].Should().Be(item1);
        ordered[0].Order.Should().Be(0);
        ordered[1].Should().Be(item3);
        ordered[1].Order.Should().Be(1, "reordered from 2 to 1");
    }

    [Fact]
    public void RemoveChecklistItem_NonExistent_ThrowsNotFoundException()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        Action act = () => entry.RemoveChecklistItem(Guid.NewGuid());

        // Assert
        act.Should().Throw<NotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void ResetChecklist_MarksAllItemsIncomplete()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        item1.MarkAsCompleted();
        item2.MarkAsCompleted();

        // Act
        entry.ResetChecklist();

        // Assert
        item1.IsCompleted.Should().BeFalse();
        item2.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void GetChecklistProgress_NoItems_Returns100Percent()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        var progress = entry.GetChecklistProgress();

        // Assert
        progress.Should().Be(100m, "no checklist means nothing to do = 100% complete");
    }

    [Fact]
    public void GetChecklistProgress_PartiallyComplete_ReturnsCorrectPercentage()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        var item3 = entry.AddChecklistItem("Step 3");
        var item4 = entry.AddChecklistItem("Step 4");

        item1.MarkAsCompleted();
        item3.MarkAsCompleted();

        // Act
        var progress = entry.GetChecklistProgress();

        // Assert
        progress.Should().Be(50m, "2 out of 4 items completed = 50%");
    }

    [Fact]
    public void IsChecklistComplete_AllCompleted_ReturnsTrue()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        item1.MarkAsCompleted();
        item2.MarkAsCompleted();

        // Act
        var isComplete = entry.IsChecklistComplete();

        // Assert
        isComplete.Should().BeTrue();
    }

    [Fact]
    public void IsChecklistComplete_NoneCompleted_ReturnsFalse()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        entry.AddChecklistItem("Step 1");

        // Act
        var isComplete = entry.IsChecklistComplete();

        // Assert
        isComplete.Should().BeFalse();
    }

    [Fact]
    public void IsChecklistComplete_NoItems_ReturnsFalse()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);

        // Act
        var isComplete = entry.IsChecklistComplete();

        // Assert
        isComplete.Should().BeFalse("no checklist items means not complete");
    }

    [Fact]
    public void GetOrderedChecklist_ReturnsItemsInOrder()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), _userId, _gameId);
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        var item3 = entry.AddChecklistItem("Step 3");

        // Act
        var ordered = entry.GetOrderedChecklist();

        // Assert
        ordered.Should().HaveCount(3);
        ordered[0].Should().Be(item1);
        ordered[1].Should().Be(item2);
        ordered[2].Should().Be(item3);
    }

    #endregion
}
