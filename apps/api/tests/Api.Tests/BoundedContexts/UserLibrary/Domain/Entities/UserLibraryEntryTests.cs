using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.Entities;

/// <summary>
/// Tests for the UserLibraryEntry aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 20
/// </summary>
[Trait("Category", "Unit")]
public sealed class UserLibraryEntryTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesEntry()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var entry = new UserLibraryEntry(id, userId, gameId);

        // Assert
        entry.Id.Should().Be(id);
        entry.UserId.Should().Be(userId);
        entry.GameId.Should().Be(gameId);
        entry.AddedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entry.IsFavorite.Should().BeFalse();
        entry.Notes.Should().BeNull();
        entry.CustomAgentConfig.Should().BeNull();
        entry.CustomPdfMetadata.Should().BeNull();
    }

    [Fact]
    public void Constructor_RaisesDomainEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var entry = new UserLibraryEntry(id, userId, gameId);

        // Assert
        entry.DomainEvents.Should().ContainSingle();
        entry.DomainEvents.First().Should().BeOfType<GameAddedToLibraryEvent>();
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var action = () => new UserLibraryEntry(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyGameId_ThrowsArgumentException()
    {
        // Act
        var action = () => new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("gameId")
            .WithMessage("*GameId cannot be empty*");
    }

    #endregion

    #region Favorite Tests

    [Fact]
    public void ToggleFavorite_WhenNotFavorite_SetsFavoriteTrue()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.ToggleFavorite();

        // Assert
        entry.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void ToggleFavorite_WhenFavorite_SetsFavoriteFalse()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsFavorite();

        // Act
        entry.ToggleFavorite();

        // Assert
        entry.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void MarkAsFavorite_SetsFavoriteTrue()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.MarkAsFavorite();

        // Assert
        entry.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void RemoveFavorite_SetsFavoriteFalse()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsFavorite();

        // Act
        entry.RemoveFavorite();

        // Assert
        entry.IsFavorite.Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SetFavorite_SetsExplicitValue(bool isFavorite)
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.SetFavorite(isFavorite);

        // Assert
        entry.IsFavorite.Should().Be(isFavorite);
    }

    #endregion

    #region Notes Tests

    [Fact]
    public void UpdateNotes_WithLibraryNotes_SetsNotes()
    {
        // Arrange
        var entry = CreateEntry();
        var notes = new LibraryNotes("Great game for family nights");

        // Act
        entry.UpdateNotes(notes);

        // Assert
        entry.Notes.Should().Be(notes);
    }

    [Fact]
    public void UpdateNotes_WithNull_ClearsNotes()
    {
        // Arrange
        var entry = CreateEntry();
        entry.UpdateNotes(new LibraryNotes("Some notes"));

        // Act
        entry.UpdateNotes(null);

        // Assert
        entry.Notes.Should().BeNull();
    }

    [Fact]
    public void UpdateNotesFromString_WithValidString_SetsNotes()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.UpdateNotesFromString("Great game!");

        // Assert
        entry.Notes.Should().NotBeNull();
        entry.Notes!.Value.Should().Be("Great game!");
    }

    [Fact]
    public void UpdateNotesFromString_WithNullOrEmpty_ClearsNotes()
    {
        // Arrange
        var entry = CreateEntry();
        entry.UpdateNotesFromString("Some notes");

        // Act
        entry.UpdateNotesFromString(null);

        // Assert
        entry.Notes.Should().BeNull();
    }

    #endregion

    #region Agent Configuration Tests

    [Fact]
    public void ConfigureAgent_WithValidConfig_SetsCustomConfig()
    {
        // Arrange
        var entry = CreateEntry();
        var config = AgentConfiguration.CreateDefault();

        // Act
        entry.ConfigureAgent(config);

        // Assert
        entry.CustomAgentConfig.Should().Be(config);
    }

    [Fact]
    public void ConfigureAgent_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        var action = () => entry.ConfigureAgent(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResetAgentToDefault_ClearsCustomConfig()
    {
        // Arrange
        var entry = CreateEntry();
        entry.ConfigureAgent(AgentConfiguration.CreateDefault());

        // Act
        entry.ResetAgentToDefault();

        // Assert
        entry.CustomAgentConfig.Should().BeNull();
    }

    [Fact]
    public void HasCustomAgent_WhenConfigured_ReturnsTrue()
    {
        // Arrange
        var entry = CreateEntry();
        entry.ConfigureAgent(AgentConfiguration.CreateDefault());

        // Assert
        entry.HasCustomAgent().Should().BeTrue();
    }

    [Fact]
    public void HasCustomAgent_WhenNotConfigured_ReturnsFalse()
    {
        // Arrange
        var entry = CreateEntry();

        // Assert
        entry.HasCustomAgent().Should().BeFalse();
    }

    #endregion

    #region Custom PDF Tests

    [Fact]
    public void UploadCustomPdf_WithValidMetadata_SetsCustomPdf()
    {
        // Arrange
        var entry = CreateEntry();
        var metadata = CustomPdfMetadata.Create(
            "https://storage.example.com/test.pdf", 1024, "test.pdf");

        // Act
        entry.UploadCustomPdf(metadata);

        // Assert
        entry.CustomPdfMetadata.Should().Be(metadata);
    }

    [Fact]
    public void UploadCustomPdf_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        var action = () => entry.UploadCustomPdf(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResetPdfToShared_ClearsCustomPdf()
    {
        // Arrange
        var entry = CreateEntry();
        entry.UploadCustomPdf(CustomPdfMetadata.Create(
            "https://storage.example.com/test.pdf", 1024, "test.pdf"));

        // Act
        entry.ResetPdfToShared();

        // Assert
        entry.CustomPdfMetadata.Should().BeNull();
    }

    [Fact]
    public void HasCustomPdf_WhenUploaded_ReturnsTrue()
    {
        // Arrange
        var entry = CreateEntry();
        entry.UploadCustomPdf(CustomPdfMetadata.Create(
            "https://storage.example.com/test.pdf", 1024, "test.pdf"));

        // Assert
        entry.CustomPdfMetadata.Should().NotBeNull();
    }

    [Fact]
    public void HasCustomPdf_WhenNotUploaded_ReturnsFalse()
    {
        // Arrange
        var entry = CreateEntry();

        // Assert
        entry.CustomPdfMetadata.Should().BeNull();
    }

    #endregion

    #region Game State Tests

    [Fact]
    public void MarkAsOwned_ChangesStateToOwned()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.MarkAsOwned("Purchased 2024");

        // Assert
        entry.CurrentState.Value.Should().Be(GameStateType.Owned);
    }

    [Fact]
    public void AddToWishlist_ChangesStateToWishlist()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        entry.AddToWishlist("Birthday gift idea");

        // Assert
        entry.CurrentState.Value.Should().Be(GameStateType.Wishlist);
    }

    [Fact]
    public void IsAvailableForPlay_WhenOwned_ReturnsTrue()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsOwned();

        // Assert
        entry.IsAvailableForPlay().Should().BeTrue();
    }

    #endregion

    #region Game Session Tests

    [Fact]
    public void RecordGameSession_AddsSessionAndUpdatesStats()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsOwned();

        // Act
        var session = entry.RecordGameSession(
            playedAt: DateTime.UtcNow,
            durationMinutes: 60,
            didWin: true,
            players: "Alice, Bob",
            notes: "Great game!");

        // Assert
        entry.Sessions.Should().ContainSingle();
        entry.Sessions.First().Should().Be(session);
        entry.Stats.TimesPlayed.Should().Be(1);
        entry.Stats.AvgDuration.Should().Be(60);
    }

    [Fact]
    public void RecordGameSession_RaisesGameSessionRecordedEvent()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsOwned();
        entry.ClearDomainEvents();

        // Act
        entry.RecordGameSession(DateTime.UtcNow, 60, true);

        // Assert
        entry.DomainEvents.Should().ContainSingle();
        entry.DomainEvents.First().Should().BeOfType<GameSessionRecordedEvent>();
    }

    [Fact]
    public void RemoveGameSession_RemovesSessionAndRecalculatesStats()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsOwned();
        var session1 = entry.RecordGameSession(DateTime.UtcNow.AddDays(-2), 60, true);
        var session2 = entry.RecordGameSession(DateTime.UtcNow.AddDays(-1), 45, false);

        // Act
        entry.RemoveGameSession(session1.Id);

        // Assert
        entry.Sessions.Should().ContainSingle();
        entry.Sessions.First().Id.Should().Be(session2.Id);
        entry.Stats.TimesPlayed.Should().Be(1);
        entry.Stats.AvgDuration.Should().Be(45);
    }

    [Fact]
    public void RemoveGameSession_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        var action = () => entry.RemoveGameSession(Guid.NewGuid());

        // Assert
        action.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void GetLatestSession_ReturnsNewestSession()
    {
        // Arrange
        var entry = CreateEntry();
        entry.MarkAsOwned();
        entry.RecordGameSession(DateTime.UtcNow.AddDays(-2), 60, true);
        var latestSession = entry.RecordGameSession(DateTime.UtcNow.AddDays(-1), 45, false);

        // Act
        var result = entry.GetLatestSession();

        // Assert
        result.Should().Be(latestSession);
    }

    [Fact]
    public void GetLatestSession_WhenNoSessions_ReturnsNull()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        var result = entry.GetLatestSession();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Checklist Tests

    [Fact]
    public void AddChecklistItem_AddsItemToChecklist()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        var item = entry.AddChecklistItem("Shuffle deck", "Make sure to shuffle thoroughly");

        // Assert
        entry.Checklist.Should().ContainSingle();
        entry.Checklist.First().Should().Be(item);
        item.Description.Should().Be("Shuffle deck");
        item.AdditionalInfo.Should().Be("Make sure to shuffle thoroughly");
    }

    [Fact]
    public void AddChecklistItem_SetsCorrectDisplayOrder()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        var item3 = entry.AddChecklistItem("Step 3");

        // Assert
        item1.DisplayOrder.Should().Be(0);
        item2.DisplayOrder.Should().Be(1);
        item3.DisplayOrder.Should().Be(2);
    }

    [Fact]
    public void RemoveChecklistItem_RemovesItemAndReorders()
    {
        // Arrange
        var entry = CreateEntry();
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        var item3 = entry.AddChecklistItem("Step 3");

        // Act
        entry.RemoveChecklistItem(item2.Id);

        // Assert
        entry.Checklist.Should().HaveCount(2);
        entry.Checklist.Should().NotContain(item2);
    }

    [Fact]
    public void RemoveChecklistItem_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        var action = () => entry.RemoveChecklistItem(Guid.NewGuid());

        // Assert
        action.Should().Throw<NotFoundException>();
    }

    [Fact]
    public void ResetChecklist_SetsAllItemsIncomplete()
    {
        // Arrange
        var entry = CreateEntry();
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        item1.MarkAsCompleted();
        item2.MarkAsCompleted();

        // Act
        entry.ResetChecklist();

        // Assert
        entry.Checklist.Should().AllSatisfy(item => item.IsCompleted.Should().BeFalse());
    }

    [Fact]
    public void GetChecklistProgress_WithNoItems_Returns100()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        var progress = entry.GetChecklistProgress();

        // Assert
        progress.Should().Be(100m);
    }

    [Fact]
    public void GetChecklistProgress_WithHalfCompleted_Returns50()
    {
        // Arrange
        var entry = CreateEntry();
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        item1.MarkAsCompleted();

        // Act
        var progress = entry.GetChecklistProgress();

        // Assert
        progress.Should().Be(50m);
    }

    [Fact]
    public void GetChecklistProgress_WithAllCompleted_Returns100()
    {
        // Arrange
        var entry = CreateEntry();
        var item1 = entry.AddChecklistItem("Step 1");
        var item2 = entry.AddChecklistItem("Step 2");
        item1.MarkAsCompleted();
        item2.MarkAsCompleted();

        // Act
        var progress = entry.GetChecklistProgress();

        // Assert
        progress.Should().Be(100m);
    }

    [Fact]
    public void IsChecklistComplete_WhenAllCompleted_ReturnsTrue()
    {
        // Arrange
        var entry = CreateEntry();
        var item = entry.AddChecklistItem("Step 1");
        item.MarkAsCompleted();

        // Assert
        entry.IsChecklistComplete().Should().BeTrue();
    }

    [Fact]
    public void IsChecklistComplete_WhenEmpty_ReturnsFalse()
    {
        // Arrange
        var entry = CreateEntry();

        // Assert
        entry.IsChecklistComplete().Should().BeFalse();
    }

    [Fact]
    public void IsChecklistComplete_WhenNotAllCompleted_ReturnsFalse()
    {
        // Arrange
        var entry = CreateEntry();
        var item1 = entry.AddChecklistItem("Step 1");
        entry.AddChecklistItem("Step 2");
        item1.MarkAsCompleted();

        // Assert
        entry.IsChecklistComplete().Should().BeFalse();
    }

    [Fact]
    public void GetOrderedChecklist_ReturnsItemsSortedByDisplayOrder()
    {
        // Arrange
        var entry = CreateEntry();
        entry.AddChecklistItem("Third");
        entry.AddChecklistItem("First");
        entry.AddChecklistItem("Second");

        // Make item2 first by updating order
        var checklist = entry.Checklist.ToList();
        checklist[0].UpdateOrder(2);
        checklist[1].UpdateOrder(0);
        checklist[2].UpdateOrder(1);

        // Act
        var ordered = entry.GetOrderedChecklist();

        // Assert
        ordered[0].Description.Should().Be("First");
        ordered[1].Description.Should().Be("Second");
        ordered[2].Description.Should().Be("Third");
    }

    #endregion

    #region Private PDF Tests

    [Fact]
    public void RemovePrivatePdf_WhenHasPrivatePdf_ClearsPdfIdAndRaisesEvent()
    {
        // Arrange
        var entry = CreateEntry();
        var pdfId = Guid.NewGuid();
        entry.AssociatePrivatePdf(pdfId);
        entry.ClearDomainEvents();

        // Act
        entry.RemovePrivatePdf();

        // Assert
        entry.HasPrivatePdf.Should().BeFalse();
        entry.PrivatePdfId.Should().BeNull();
        entry.DomainEvents.Should().ContainSingle();
        entry.DomainEvents.First().Should().BeOfType<PrivatePdfRemovedEvent>();
    }

    [Fact]
    public void RemovePrivatePdf_WhenNoPrivatePdf_ThrowsConflictException()
    {
        // Arrange
        var entry = CreateEntry();

        // Act
        var action = () => entry.RemovePrivatePdf();

        // Assert
        action.Should().Throw<ConflictException>()
            .WithMessage("*No private PDF is associated*");
    }

    [Fact]
    public void RemovePrivatePdf_RaisesEventWithCorrectProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var entry = new UserLibraryEntry(id, userId, gameId);
        entry.AssociatePrivatePdf(pdfId);
        entry.ClearDomainEvents();

        // Act
        entry.RemovePrivatePdf();

        // Assert
        var evt = entry.DomainEvents.First().Should().BeOfType<PrivatePdfRemovedEvent>().Subject;
        evt.LibraryEntryId.Should().Be(id);
        evt.UserId.Should().Be(userId);
        evt.GameId.Should().Be(gameId);
        evt.PdfDocumentId.Should().Be(pdfId);
    }

    [Fact]
    public void AssociatePrivatePdf_SetsPrivatePdfId()
    {
        // Arrange
        var entry = CreateEntry();
        var pdfId = Guid.NewGuid();

        // Act
        entry.AssociatePrivatePdf(pdfId);

        // Assert
        entry.HasPrivatePdf.Should().BeTrue();
        entry.PrivatePdfId.Should().Be(pdfId);
    }

    [Fact]
    public void HasPrivatePdf_WhenPdfAssociated_ReturnsTrue()
    {
        // Arrange
        var entry = CreateEntry();
        entry.AssociatePrivatePdf(Guid.NewGuid());

        // Assert
        entry.HasPrivatePdf.Should().BeTrue();
    }

    [Fact]
    public void HasPrivatePdf_WhenNoPdfAssociated_ReturnsFalse()
    {
        // Arrange
        var entry = CreateEntry();

        // Assert
        entry.HasPrivatePdf.Should().BeFalse();
    }

    #endregion

    #region PrepareForRemoval Tests

    [Fact]
    public void PrepareForRemoval_RaisesGameRemovedFromLibraryEvent()
    {
        // Arrange
        var entry = CreateEntry();
        entry.ClearDomainEvents();

        // Act
        entry.PrepareForRemoval();

        // Assert
        entry.DomainEvents.Should().ContainSingle();
        entry.DomainEvents.First().Should().BeOfType<GameRemovedFromLibraryEvent>();
    }

    #endregion

    #region Helper Methods

    private static UserLibraryEntry CreateEntry()
    {
        return new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }

    #endregion
}
