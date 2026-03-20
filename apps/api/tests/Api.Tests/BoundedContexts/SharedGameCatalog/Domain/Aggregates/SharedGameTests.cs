using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Tests for the SharedGame aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 21 PR#5
/// </summary>
[Trait("Category", "Unit")]
public sealed class SharedGameTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_CreatesGame()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var rules = GameRules.Create("Basic game rules", "en");

        // Act
        var game = SharedGame.Create(
            "Catan",
            2010,
            "Strategic resource management game",
            3,
            4,
            90,
            10,
            2.5m,
            7.8m,
            "https://example.com/catan.jpg",
            "https://example.com/catan-thumb.jpg",
            rules,
            createdBy,
            12345);

        // Assert
        game.Id.Should().NotBe(Guid.Empty);
        game.Title.Should().Be("Catan");
        game.YearPublished.Should().Be(2010);
        game.Description.Should().Be("Strategic resource management game");
        game.MinPlayers.Should().Be(3);
        game.MaxPlayers.Should().Be(4);
        game.PlayingTimeMinutes.Should().Be(90);
        game.MinAge.Should().Be(10);
        game.ComplexityRating.Should().Be(2.5m);
        game.AverageRating.Should().Be(7.8m);
        game.ImageUrl.Should().Be("https://example.com/catan.jpg");
        game.ThumbnailUrl.Should().Be("https://example.com/catan-thumb.jpg");
        game.Rules.Should().Be(rules);
        game.Status.Should().Be(GameStatus.Draft);
        game.IsDeleted.Should().BeFalse();
        game.CreatedBy.Should().Be(createdBy);
        game.BggId.Should().Be(12345);
        game.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithoutBggId_CreatesGameWithNullBggId()
    {
        // Act
        var game = CreateValidGame();

        // Assert
        game.BggId.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullRules_CreatesGameWithNullRules()
    {
        // Act
        var game = SharedGame.Create(
            "Simple Game",
            2020,
            "A simple game without rules",
            2,
            6,
            30,
            8,
            null,
            null,
            "https://example.com/game.jpg",
            "https://example.com/game-thumb.jpg",
            null,
            Guid.NewGuid());

        // Assert
        game.Rules.Should().BeNull();
    }

    [Fact]
    public void Create_RaisesDomainEvent()
    {
        // Arrange
        var createdBy = Guid.NewGuid();

        // Act
        var game = CreateValidGame(createdBy: createdBy);

        // Assert
        var events = game.DomainEvents;
        events.Should().ContainSingle();
        events.First().Should().BeOfType<SharedGameCreatedEvent>();
        var evt = (SharedGameCreatedEvent)events.First();
        evt.GameId.Should().Be(game.Id);
        evt.Title.Should().Be(game.Title);
        evt.CreatedBy.Should().Be(createdBy);
    }

    #endregion

    #region Create Validation Tests

    [Fact]
    public void Create_WithEmptyTitle_ThrowsArgumentException()
    {
        // Act
        var action = () => SharedGame.Create(
            "",
            2020,
            "Description",
            2,
            4,
            60,
            10,
            null,
            null,
            "https://example.com/img.jpg",
            "https://example.com/thumb.jpg",
            null,
            Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("title")
            .WithMessage("*Title is required*");
    }

    [Fact]
    public void Create_WithTitleTooLong_ThrowsArgumentException()
    {
        // Act
        var action = () => SharedGame.Create(
            new string('A', 501),
            2020,
            "Description",
            2,
            4,
            60,
            10,
            null,
            null,
            "https://example.com/img.jpg",
            "https://example.com/thumb.jpg",
            null,
            Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Title cannot exceed 500 characters*");
    }

    [Fact]
    public void Create_WithYearTooOld_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(yearPublished: 1900);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Year must be greater than 1900*");
    }

    [Fact]
    public void Create_WithYearTooFarInFuture_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(yearPublished: DateTime.UtcNow.Year + 2);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage($"*Year cannot exceed {DateTime.UtcNow.Year + 1}*");
    }

    [Fact]
    public void Create_WithEmptyDescription_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(description: "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description is required*");
    }

    [Fact]
    public void Create_WithZeroMinPlayers_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(minPlayers: 0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MinPlayers must be greater than 0*");
    }

    [Fact]
    public void Create_WithMaxPlayersLessThanMin_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(minPlayers: 4, maxPlayers: 2);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MaxPlayers must be greater than or equal to MinPlayers*");
    }

    [Fact]
    public void Create_WithZeroPlayingTime_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(playingTimeMinutes: 0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*PlayingTime must be greater than 0*");
    }

    [Fact]
    public void Create_WithNegativeMinAge_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(minAge: -1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MinAge cannot be negative*");
    }

    [Fact]
    public void Create_WithComplexityRatingTooLow_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(complexityRating: 0.5m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ComplexityRating must be between 1.0 and 5.0*");
    }

    [Fact]
    public void Create_WithComplexityRatingTooHigh_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(complexityRating: 5.5m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ComplexityRating must be between 1.0 and 5.0*");
    }

    [Fact]
    public void Create_WithAverageRatingTooLow_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(averageRating: 0.5m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*AverageRating must be between 1.0 and 10.0*");
    }

    [Fact]
    public void Create_WithAverageRatingTooHigh_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(averageRating: 10.5m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*AverageRating must be between 1.0 and 10.0*");
    }

    [Fact]
    public void Create_WithInvalidImageUrl_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(imageUrl: "not-a-url");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ImageUrl must be a valid URL*");
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Act
        var action = () => CreateValidGame(createdBy: Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*CreatedBy cannot be empty*");
    }

    #endregion

    #region UpdateInfo Tests

    [Fact]
    public void UpdateInfo_WithValidParameters_UpdatesAllFields()
    {
        // Arrange
        var game = CreateValidGame();
        var modifiedBy = Guid.NewGuid();
        var newRules = GameRules.Create("Updated rules content", "en");

        // Act
        game.UpdateInfo(
            "Updated Title",
            2022,
            "Updated description",
            2,
            6,
            120,
            12,
            3.5m,
            8.5m,
            "https://example.com/new.jpg",
            "https://example.com/new-thumb.jpg",
            newRules,
            modifiedBy);

        // Assert
        game.Title.Should().Be("Updated Title");
        game.YearPublished.Should().Be(2022);
        game.Description.Should().Be("Updated description");
        game.MinPlayers.Should().Be(2);
        game.MaxPlayers.Should().Be(6);
        game.PlayingTimeMinutes.Should().Be(120);
        game.MinAge.Should().Be(12);
        game.ComplexityRating.Should().Be(3.5m);
        game.AverageRating.Should().Be(8.5m);
        game.ImageUrl.Should().Be("https://example.com/new.jpg");
        game.ThumbnailUrl.Should().Be("https://example.com/new-thumb.jpg");
        game.Rules.Should().Be(newRules);
        game.ModifiedBy.Should().Be(modifiedBy);
        game.ModifiedAt.Should().NotBeNull();
        game.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateInfo_RaisesDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        var modifiedBy = Guid.NewGuid();
        game.ClearDomainEvents();

        // Act
        game.UpdateInfo(
            "Updated Title",
            2022,
            "Updated description",
            2,
            6,
            120,
            12,
            null,
            null,
            "https://example.com/new.jpg",
            "https://example.com/new-thumb.jpg",
            null,
            modifiedBy);

        // Assert
        var events = game.DomainEvents;
        events.Should().ContainSingle();
        events.First().Should().BeOfType<SharedGameUpdatedEvent>();
        var evt = (SharedGameUpdatedEvent)events.First();
        evt.GameId.Should().Be(game.Id);
        evt.ModifiedBy.Should().Be(modifiedBy);
    }

    [Fact]
    public void UpdateInfo_WithEmptyModifiedBy_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.UpdateInfo(
            "Title",
            2020,
            "Description",
            2,
            4,
            60,
            10,
            null,
            null,
            "https://example.com/img.jpg",
            "https://example.com/thumb.jpg",
            null,
            Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ModifiedBy cannot be empty*");
    }

    #endregion

    #region Status Workflow Tests - Submit/Approve/Reject

    [Fact]
    public void SubmitForApproval_FromDraft_TransitionsToPendingApproval()
    {
        // Arrange
        var game = CreateValidGame();
        var submittedBy = Guid.NewGuid();
        game.Status.Should().Be(GameStatus.Draft);

        // Act
        game.SubmitForApproval(submittedBy);

        // Assert
        game.Status.Should().Be(GameStatus.PendingApproval);
        game.ModifiedBy.Should().Be(submittedBy);
        game.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void SubmitForApproval_RaisesDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        var submittedBy = Guid.NewGuid();
        game.ClearDomainEvents();

        // Act
        game.SubmitForApproval(submittedBy);

        // Assert
        var events = game.DomainEvents;
        events.Should().ContainSingle();
        events.First().Should().BeOfType<SharedGameSubmittedForApprovalEvent>();
        var evt = (SharedGameSubmittedForApprovalEvent)events.First();
        evt.GameId.Should().Be(game.Id);
        evt.SubmittedBy.Should().Be(submittedBy);
    }

    [Fact]
    public void SubmitForApproval_WhenNotDraft_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(Guid.NewGuid());
        game.Status.Should().Be(GameStatus.PendingApproval);

        // Act
        var action = () => game.SubmitForApproval(Guid.NewGuid());

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot submit game for approval in PendingApproval status*");
    }

    [Fact]
    public void SubmitForApproval_WithEmptySubmittedBy_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.SubmitForApproval(Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SubmittedBy cannot be empty*");
    }

    [Fact]
    public void ApprovePublication_FromPendingApproval_TransitionsToPublished()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(Guid.NewGuid());
        var approvedBy = Guid.NewGuid();

        // Act
        game.ApprovePublication(approvedBy);

        // Assert
        game.Status.Should().Be(GameStatus.Published);
        game.ModifiedBy.Should().Be(approvedBy);
    }

    [Fact]
    public void ApprovePublication_RaisesDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(Guid.NewGuid());
        var approvedBy = Guid.NewGuid();
        game.ClearDomainEvents();

        // Act
        game.ApprovePublication(approvedBy);

        // Assert
        var events = game.DomainEvents;
        events.Should().ContainSingle();
        events.First().Should().BeOfType<SharedGamePublicationApprovedEvent>();
        var evt = (SharedGamePublicationApprovedEvent)events.First();
        evt.GameId.Should().Be(game.Id);
        evt.ApprovedBy.Should().Be(approvedBy);
    }

    [Fact]
    public void ApprovePublication_WhenNotPendingApproval_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        game.Status.Should().Be(GameStatus.Draft);

        // Act
        var action = () => game.ApprovePublication(Guid.NewGuid());

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot approve publication in Draft status*");
    }

    [Fact]
    public void ApprovePublication_WithEmptyApprovedBy_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(Guid.NewGuid());

        // Act
        var action = () => game.ApprovePublication(Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ApprovedBy cannot be empty*");
    }

    [Fact]
    public void RejectPublication_FromPendingApproval_TransitionsBackToDraft()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(Guid.NewGuid());
        var rejectedBy = Guid.NewGuid();

        // Act
        game.RejectPublication(rejectedBy, "Needs more images");

        // Assert
        game.Status.Should().Be(GameStatus.Draft);
        game.ModifiedBy.Should().Be(rejectedBy);
    }

    [Fact]
    public void RejectPublication_RaisesDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(Guid.NewGuid());
        var rejectedBy = Guid.NewGuid();
        game.ClearDomainEvents();

        // Act
        game.RejectPublication(rejectedBy, "Incomplete information");

        // Assert
        var events = game.DomainEvents;
        events.Should().ContainSingle();
        events.First().Should().BeOfType<SharedGamePublicationRejectedEvent>();
        var evt = (SharedGamePublicationRejectedEvent)events.First();
        evt.GameId.Should().Be(game.Id);
        evt.RejectedBy.Should().Be(rejectedBy);
        evt.Reason.Should().Be("Incomplete information");
    }

    [Fact]
    public void RejectPublication_WhenNotPendingApproval_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.RejectPublication(Guid.NewGuid(), "Some reason");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot reject publication in Draft status*");
    }

    [Fact]
    public void RejectPublication_WithEmptyReason_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(Guid.NewGuid());

        // Act
        var action = () => game.RejectPublication(Guid.NewGuid(), "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Rejection reason is required*");
    }

    [Fact]
    public void RejectPublication_WithEmptyRejectedBy_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(Guid.NewGuid());

        // Act
        var action = () => game.RejectPublication(Guid.Empty, "Some reason");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*RejectedBy cannot be empty*");
    }

    #endregion

    #region Archive Tests

    [Fact]
    public void Archive_FromPublished_TransitionsToArchived()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(Guid.NewGuid());
        game.ApprovePublication(Guid.NewGuid());
        var archivedBy = Guid.NewGuid();

        // Act
        game.Archive(archivedBy);

        // Assert
        game.Status.Should().Be(GameStatus.Archived);
        game.ModifiedBy.Should().Be(archivedBy);
    }

    [Fact]
    public void Archive_RaisesDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        var archivedBy = Guid.NewGuid();
        game.ClearDomainEvents();

        // Act
        game.Archive(archivedBy);

        // Assert
        var events = game.DomainEvents;
        events.Should().ContainSingle();
        events.First().Should().BeOfType<SharedGameArchivedEvent>();
        var evt = (SharedGameArchivedEvent)events.First();
        evt.GameId.Should().Be(game.Id);
        evt.ArchivedBy.Should().Be(archivedBy);
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        game.Archive(Guid.NewGuid());

        // Act
        var action = () => game.Archive(Guid.NewGuid());

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Game is already archived*");
    }

    [Fact]
    public void Archive_WithEmptyArchivedBy_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.Archive(Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ArchivedBy cannot be empty*");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public void Delete_SetsIsDeletedFlag()
    {
        // Arrange
        var game = CreateValidGame();
        var deletedBy = Guid.NewGuid();

        // Act
        game.Delete(deletedBy);

        // Assert
        game.IsDeleted.Should().BeTrue();
        game.ModifiedBy.Should().Be(deletedBy);
        game.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void Delete_RaisesDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        var deletedBy = Guid.NewGuid();
        game.ClearDomainEvents();

        // Act
        game.Delete(deletedBy);

        // Assert
        var events = game.DomainEvents;
        events.Should().ContainSingle();
        events.First().Should().BeOfType<SharedGameDeletedEvent>();
        var evt = (SharedGameDeletedEvent)events.First();
        evt.GameId.Should().Be(game.Id);
        evt.DeletedBy.Should().Be(deletedBy);
    }

    [Fact]
    public void Delete_WhenAlreadyDeleted_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        game.Delete(Guid.NewGuid());

        // Act
        var action = () => game.Delete(Guid.NewGuid());

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Game is already deleted*");
    }

    [Fact]
    public void Delete_WithEmptyDeletedBy_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.Delete(Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*DeletedBy cannot be empty*");
    }

    #endregion

    #region FAQ Tests

    [Fact]
    public void AddFaq_WithValidFaq_AddsFaqToCollection()
    {
        // Arrange
        var game = CreateValidGame();
        var faq = new GameFaq(Guid.NewGuid(), game.Id, "How to play?", "Follow the rules", 1, 0, DateTime.UtcNow, null);

        // Act
        game.AddFaq(faq);

        // Assert
        game.Faqs.Should().ContainSingle();
        game.Faqs.First().Should().Be(faq);
    }

    [Fact]
    public void AddFaq_RaisesDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        var faq = new GameFaq(Guid.NewGuid(), game.Id, "Question?", "Answer", 1, 0, DateTime.UtcNow, null);
        game.ClearDomainEvents();

        // Act
        game.AddFaq(faq);

        // Assert
        var events = game.DomainEvents;
        events.Should().ContainSingle();
        events.First().Should().BeOfType<GameFaqAddedEvent>();
    }

    [Fact]
    public void AddFaq_WithNullFaq_ThrowsArgumentNullException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.AddFaq(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddFaq_WithMismatchedGameId_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();
        var faq = new GameFaq(Guid.NewGuid(), Guid.NewGuid(), "Question?", "Answer", 1, 0, DateTime.UtcNow, null);

        // Act
        var action = () => game.AddFaq(faq);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*FAQ does not belong to this game*");
    }

    [Fact]
    public void UpdateFaq_WithValidParameters_UpdatesFaq()
    {
        // Arrange
        var game = CreateValidGame();
        var faq = new GameFaq(Guid.NewGuid(), game.Id, "Old question", "Old answer", 1, 0, DateTime.UtcNow, null);
        game.AddFaq(faq);

        // Act
        game.UpdateFaq(faq.Id, "New question", "New answer", 2);

        // Assert
        var updatedFaq = game.Faqs.First();
        updatedFaq.Question.Should().Be("New question");
        updatedFaq.Answer.Should().Be("New answer");
        updatedFaq.DisplayOrder.Should().Be(2);
    }

    [Fact]
    public void UpdateFaq_WithNonExistentFaqId_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var action = () => game.UpdateFaq(Guid.NewGuid(), "Q", "A", 1);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*FAQ*not found*");
    }

    [Fact]
    public void UpvoteFaq_IncreasesUpvoteCount()
    {
        // Arrange
        var game = CreateValidGame();
        var faq = new GameFaq(Guid.NewGuid(), game.Id, "Question", "Answer", 1, 0, DateTime.UtcNow, null);
        game.AddFaq(faq);

        // Act
        var newCount = game.UpvoteFaq(faq.Id);

        // Assert
        newCount.Should().Be(1);
        game.Faqs.First().UpvoteCount.Should().Be(1);
    }

    [Fact]
    public void RemoveFaq_RemovesFaqFromCollection()
    {
        // Arrange
        var game = CreateValidGame();
        var faq = new GameFaq(Guid.NewGuid(), game.Id, "Question", "Answer", 1, 0, DateTime.UtcNow, null);
        game.AddFaq(faq);

        // Act
        game.RemoveFaq(faq.Id);

        // Assert
        game.Faqs.Should().BeEmpty();
    }

    #endregion

    #region Errata Tests

    [Fact]
    public void AddErrata_WithValidErrata_AddsErrataToCollection()
    {
        // Arrange
        var game = CreateValidGame();
        var errata = new GameErrata(Guid.NewGuid(), game.Id, "Typo in rulebook", "Page 5", DateTime.UtcNow, DateTime.UtcNow);

        // Act
        game.AddErrata(errata);

        // Assert
        game.Erratas.Should().ContainSingle();
        game.Erratas.First().Should().Be(errata);
    }

    [Fact]
    public void AddErrata_RaisesDomainEvent()
    {
        // Arrange
        var game = CreateValidGame();
        var errata = new GameErrata(Guid.NewGuid(), game.Id, "Error description", "Page 1", DateTime.UtcNow, DateTime.UtcNow);
        game.ClearDomainEvents();

        // Act
        game.AddErrata(errata);

        // Assert
        var events = game.DomainEvents;
        events.Should().ContainSingle();
        events.First().Should().BeOfType<GameErrataAddedEvent>();
    }

    [Fact]
    public void UpdateErrata_WithValidParameters_UpdatesErrata()
    {
        // Arrange
        var game = CreateValidGame();
        var errata = new GameErrata(Guid.NewGuid(), game.Id, "Old description", "Page 1", DateTime.UtcNow, DateTime.UtcNow);
        game.AddErrata(errata);

        // Act
        var newDate = DateTime.UtcNow.AddDays(-1);
        game.UpdateErrata(errata.Id, "New description", "Page 2", newDate);

        // Assert
        var updated = game.Erratas.First();
        updated.Description.Should().Be("New description");
        updated.PageReference.Should().Be("Page 2");
        updated.PublishedDate.Should().Be(newDate);
    }

    [Fact]
    public void RemoveErrata_RemovesErrataFromCollection()
    {
        // Arrange
        var game = CreateValidGame();
        var errata = new GameErrata(Guid.NewGuid(), game.Id, "Description", "Page 1", DateTime.UtcNow, DateTime.UtcNow);
        game.AddErrata(errata);

        // Act
        game.RemoveErrata(errata.Id);

        // Assert
        game.Erratas.Should().BeEmpty();
    }

    #endregion

    #region QuickQuestion Tests

    [Fact]
    public void AddQuickQuestion_AddsToCollection()
    {
        // Arrange
        var game = CreateValidGame();
        var question = new QuickQuestion(Guid.NewGuid(), game.Id, "What happens if...?", "❓", QuestionCategory.Rules, 1, true, DateTime.UtcNow, true);

        // Act
        game.AddQuickQuestion(question);

        // Assert
        game.QuickQuestions.Should().ContainSingle();
        game.QuickQuestions.First().Should().Be(question);
    }

    [Fact]
    public void RemoveQuickQuestion_RemovesFromCollection()
    {
        // Arrange
        var game = CreateValidGame();
        var question = new QuickQuestion(Guid.NewGuid(), game.Id, "Question?", "❓", QuestionCategory.Setup, 1, true, DateTime.UtcNow, true);
        game.AddQuickQuestion(question);

        // Act
        game.RemoveQuickQuestion(question.Id);

        // Assert
        game.QuickQuestions.Should().BeEmpty();
    }

    [Fact]
    public void ReplaceQuickQuestions_ClearsAndReplacesAll()
    {
        // Arrange
        var game = CreateValidGame();
        var oldQuestion = new QuickQuestion(Guid.NewGuid(), game.Id, "Old?", "❓", QuestionCategory.Rules, 1, true, DateTime.UtcNow, true);
        game.AddQuickQuestion(oldQuestion);

        var newQuestions = new[]
        {
            new QuickQuestion(Guid.NewGuid(), game.Id, "New 1?", "❓", QuestionCategory.Setup, 1, true, DateTime.UtcNow, true),
            new QuickQuestion(Guid.NewGuid(), game.Id, "New 2?", "❓", QuestionCategory.Strategy, 2, true, DateTime.UtcNow, true)
        };

        // Act
        game.ReplaceQuickQuestions(newQuestions);

        // Assert
        game.QuickQuestions.Should().HaveCount(2);
        game.QuickQuestions.Should().NotContain(oldQuestion);
        game.QuickQuestions.Should().Contain(newQuestions[0]);
        game.QuickQuestions.Should().Contain(newQuestions[1]);
    }

    #endregion

    #region Contributor Tests

    [Fact]
    public void AddContributor_WithValidParameters_AddsContributor()
    {
        // Arrange
        var game = CreateValidGame();
        var userId = Guid.NewGuid();

        // Act
        var contributor = game.AddContributor(userId, isPrimary: false);

        // Assert
        contributor.Should().NotBeNull();
        contributor.UserId.Should().Be(userId);
        contributor.IsPrimaryContributor.Should().BeFalse();
        game.ContributorCount.Should().Be(1);
        game.IsContributor(userId).Should().BeTrue();
    }

    [Fact]
    public void AddContributor_AsPrimary_SetsPrimaryFlag()
    {
        // Arrange
        var game = CreateValidGame();
        var userId = Guid.NewGuid();

        // Act
        var contributor = game.AddContributor(userId, isPrimary: true);

        // Assert
        contributor.IsPrimaryContributor.Should().BeTrue();
        game.GetPrimaryContributor().Should().Be(contributor);
    }

    [Fact]
    public void AddContributor_WhenUserAlreadyContributor_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        var userId = Guid.NewGuid();
        game.AddContributor(userId, false);

        // Act
        var action = () => game.AddContributor(userId, false);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*User*already a contributor*");
    }

    [Fact]
    public void AddContributor_WhenPrimaryAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        game.AddContributor(Guid.NewGuid(), isPrimary: true);

        // Act
        var action = () => game.AddContributor(Guid.NewGuid(), isPrimary: true);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*primary contributor already exists*");
    }

    [Fact]
    public void AddPrimaryContributorWithInitialSubmission_CreatesContributorWithSubmission()
    {
        // Arrange
        var game = CreateValidGame();
        var userId = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var contributor = game.AddPrimaryContributorWithInitialSubmission(userId, shareRequestId, documentIds);

        // Assert
        contributor.IsPrimaryContributor.Should().BeTrue();
        contributor.UserId.Should().Be(userId);
        game.GetPrimaryContributor().Should().Be(contributor);
    }

    [Fact]
    public void GetContributorByUserId_WhenExists_ReturnsContributor()
    {
        // Arrange
        var game = CreateValidGame();
        var userId = Guid.NewGuid();
        var added = game.AddContributor(userId, false);

        // Act
        var found = game.GetContributorByUserId(userId);

        // Assert
        found.Should().Be(added);
    }

    [Fact]
    public void GetAllContributors_ReturnsAllContributors()
    {
        // Arrange
        var game = CreateValidGame();
        var user1 = game.AddContributor(Guid.NewGuid(), true);
        var user2 = game.AddContributor(Guid.NewGuid(), false);

        // Act
        var all = game.GetAllContributors();

        // Assert
        all.Should().HaveCount(2);
        all.Should().Contain(user1);
        all.Should().Contain(user2);
    }

    #endregion

    #region Complete Workflow Tests

    [Fact]
    public void CompletePublicationWorkflow_Success()
    {
        // Arrange
        var game = CreateValidGame();
        var submitter = Guid.NewGuid();
        var approver = Guid.NewGuid();

        // Act & Assert - Draft to PendingApproval
        game.Status.Should().Be(GameStatus.Draft);
        game.SubmitForApproval(submitter);
        game.Status.Should().Be(GameStatus.PendingApproval);

        // PendingApproval to Published
        game.ApprovePublication(approver);
        game.Status.Should().Be(GameStatus.Published);
    }

    [Fact]
    public void CompleteRejectionWorkflow_ReturnsToAndResubmits()
    {
        // Arrange
        var game = CreateValidGame();
        var submitter = Guid.NewGuid();
        var rejector = Guid.NewGuid();

        // Act & Assert
        game.SubmitForApproval(submitter);
        game.Status.Should().Be(GameStatus.PendingApproval);

        game.RejectPublication(rejector, "Needs improvement");
        game.Status.Should().Be(GameStatus.Draft);

        // Can resubmit after rejection
        game.SubmitForApproval(submitter);
        game.Status.Should().Be(GameStatus.PendingApproval);
    }

    #endregion

    #region Helper Methods

    private static SharedGame CreateValidGame(
        string title = "Test Game",
        int yearPublished = 2020,
        string description = "Test description",
        int minPlayers = 2,
        int maxPlayers = 4,
        int playingTimeMinutes = 60,
        int minAge = 10,
        decimal? complexityRating = 2.5m,
        decimal? averageRating = 7.5m,
        string imageUrl = "https://example.com/image.jpg",
        string thumbnailUrl = "https://example.com/thumb.jpg",
        Guid? createdBy = null)
    {
        return SharedGame.Create(
            title,
            yearPublished,
            description,
            minPlayers,
            maxPlayers,
            playingTimeMinutes,
            minAge,
            complexityRating,
            averageRating,
            imageUrl,
            thumbnailUrl,
            null,
            createdBy ?? Guid.NewGuid());
    }

    #endregion

    #region Agent Linking Tests (Issue #4228)

    [Fact]
    public void LinkAgent_WithValidAgentId_LinksAgent()
    {
        // Arrange
        var game = CreateValidGame();
        var agentId = Guid.NewGuid();

        // Act
        game.LinkAgent(agentId);

        // Assert
        game.AgentDefinitionId.Should().Be(agentId);
        game.DomainEvents.Should().ContainSingle(e => e is AgentLinkedToSharedGameEvent);
        var domainEvent = game.DomainEvents.OfType<AgentLinkedToSharedGameEvent>().Single();
        domainEvent.GameId.Should().Be(game.Id);
        domainEvent.AgentDefinitionId.Should().Be(agentId);
    }

    [Fact]
    public void LinkAgent_WhenAgentAlreadyLinked_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        var firstAgentId = Guid.NewGuid();
        var secondAgentId = Guid.NewGuid();
        game.LinkAgent(firstAgentId);

        // Act
        var act = () => game.LinkAgent(secondAgentId);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("An agent is already linked to this game");
        game.AgentDefinitionId.Should().Be(firstAgentId); // Should not change
    }

    [Fact]
    public void LinkAgent_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var act = () => game.LinkAgent(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*AgentId cannot be empty*");
    }

    [Fact]
    public void UnlinkAgent_WhenAgentLinked_UnlinksAgent()
    {
        // Arrange
        var game = CreateValidGame();
        var agentId = Guid.NewGuid();
        game.LinkAgent(agentId);
        game.ClearDomainEvents(); // Clear link event

        // Act
        game.UnlinkAgent();

        // Assert
        game.AgentDefinitionId.Should().BeNull();
        game.DomainEvents.Should().ContainSingle(e => e is AgentUnlinkedFromSharedGameEvent);
        var domainEvent = game.DomainEvents.OfType<AgentUnlinkedFromSharedGameEvent>().Single();
        domainEvent.GameId.Should().Be(game.Id);
        domainEvent.AgentDefinitionId.Should().Be(agentId);
    }

    [Fact]
    public void UnlinkAgent_WhenNoAgentLinked_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();

        // Act
        var act = () => game.UnlinkAgent();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("No agent is currently linked to this game");
    }

    #endregion
}