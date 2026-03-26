using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain;

[Trait("Category", TestCategories.Unit)]
public class SharedGameDomainTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidData_CreatesSharedGameSuccessfully()
    {
        // Arrange & Act
        var game = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "A classic resource management game",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.8m,
            imageUrl: "https://example.com/catan.jpg",
            thumbnailUrl: "https://example.com/catan-thumb.jpg",
            rules: GameRules.Create("Game rules content", "en"),
            createdBy: TestUserId,
            bggId: 13);

        // Assert
        game.Id.Should().NotBe(Guid.Empty);
        game.Title.Should().Be("Catan");
        game.YearPublished.Should().Be(1995);
        game.MinPlayers.Should().Be(3);
        game.MaxPlayers.Should().Be(4);
        game.PlayingTimeMinutes.Should().Be(90);
        game.MinAge.Should().Be(10);
        game.ComplexityRating.Should().Be(2.5m);
        game.AverageRating.Should().Be(7.8m);
        game.BggId.Should().Be(13);
        game.Status.Should().Be(GameStatus.Draft);
        game.CreatedBy.Should().Be(TestUserId);
        game.Rules.Should().NotBeNull();
        game.Rules.Content.Should().Be("Game rules content");
        game.Rules.Language.Should().Be("en");

        // Assert domain event raised
        game.DomainEvents.Should().ContainSingle();
        var createdEvent = game.DomainEvents.First().Should().BeOfType<SharedGameCreatedEvent>().Subject;
        createdEvent.GameId.Should().Be(game.Id);
        createdEvent.Title.Should().Be("Catan");
        createdEvent.CreatedBy.Should().Be(TestUserId);
    }

    [Fact]
    public void UpdateInfo_WithValidData_UpdatesGameSuccessfully()
    {
        // Arrange
        var game = CreateValidGame();
        var modifierId = Guid.NewGuid();

        // Act
        game.UpdateInfo(
            title: "Settlers of Catan",
            yearPublished: 1995,
            description: "Updated description",
            minPlayers: 2,
            maxPlayers: 6,
            playingTimeMinutes: 120,
            minAge: 12,
            complexityRating: 3.0m,
            averageRating: 8.0m,
            imageUrl: "https://example.com/new.jpg",
            thumbnailUrl: "https://example.com/new-thumb.jpg",
            rules: GameRules.Create("Updated rules", "it"),
            modifiedBy: modifierId);

        // Assert
        game.Title.Should().Be("Settlers of Catan");
        game.MinPlayers.Should().Be(2);
        game.MaxPlayers.Should().Be(6);
        game.PlayingTimeMinutes.Should().Be(120);
        game.MinAge.Should().Be(12);
        game.ComplexityRating.Should().Be(3.0m);
        game.AverageRating.Should().Be(8.0m);
        game.ModifiedBy.Should().Be(modifierId);
        game.ModifiedAt.Should().NotBeNull();
        game.Rules.Should().NotBeNull();
        game.Rules.Content.Should().Be("Updated rules");
        game.Rules.Language.Should().Be("it");

        // Assert domain event raised (CreatedEvent + UpdatedEvent)
        game.DomainEvents.Count.Should().Be(2);
        var updatedEvent = game.DomainEvents.Last().Should().BeOfType<SharedGameUpdatedEvent>().Subject;
        updatedEvent.GameId.Should().Be(game.Id);
        updatedEvent.ModifiedBy.Should().Be(modifierId);
    }

    [Theory]
    [InlineData("", 1995, "Description", 1, 4, 90, 10)] // Empty title
    [InlineData("Catan", 1800, "Description", 1, 4, 90, 10)] // Year too old
    [InlineData("Catan", 2030, "Description", 1, 4, 90, 10)] // Year too far future
    [InlineData("Catan", 1995, "", 1, 4, 90, 10)] // Empty description
    [InlineData("Catan", 1995, "Description", 0, 4, 90, 10)] // MinPlayers = 0
    [InlineData("Catan", 1995, "Description", 5, 2, 90, 10)] // MaxPlayers < MinPlayers
    [InlineData("Catan", 1995, "Description", 1, 4, 0, 10)] // PlayingTime = 0
    [InlineData("Catan", 1995, "Description", 1, 4, 90, -1)] // MinAge negative
    public void Create_WithInvalidData_ThrowsArgumentException(
        string title,
        int year,
        string description,
        int minPlayers,
        int maxPlayers,
        int playingTime,
        int minAge)
    {
        // Act & Assert
        var act = () =>
            SharedGame.Create(
                title,
                year,
                description,
                minPlayers,
                maxPlayers,
                playingTime,
                minAge,
                null,
                null,
                "https://example.com/image.jpg",
                "https://example.com/thumb.jpg",
                null,
                TestUserId,
                null);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0.5)] // Below 1.0
    [InlineData(5.5)] // Above 5.0
    public void Create_WithInvalidComplexityRating_ThrowsArgumentException(decimal rating)
    {
        // Act & Assert
        var act = () =>
            SharedGame.Create(
                "Catan",
                1995,
                "Description",
                3,
                4,
                90,
                10,
                rating,
                null,
                "https://example.com/image.jpg",
                "https://example.com/thumb.jpg",
                null,
                TestUserId,
                null);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0.5)] // Below 1.0
    [InlineData(10.5)] // Above 10.0
    public void Create_WithInvalidAverageRating_ThrowsArgumentException(decimal rating)
    {
        // Act & Assert
        var act = () =>
            SharedGame.Create(
                "Catan",
                1995,
                "Description",
                3,
                4,
                90,
                10,
                null,
                rating,
                "https://example.com/image.jpg",
                "https://example.com/thumb.jpg",
                null,
                TestUserId,
                null);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("relative/path.jpg")]
    [InlineData("")]
    public void Create_WithInvalidImageUrl_ThrowsArgumentException(string url)
    {
        // Act & Assert
        var act = () =>
            SharedGame.Create(
                "Catan",
                1995,
                "Description",
                3,
                4,
                90,
                10,
                null,
                null,
                url,
                "https://example.com/thumb.jpg",
                null,
                TestUserId,
                null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () =>
            SharedGame.Create(
                "Catan",
                1995,
                "Description",
                3,
                4,
                90,
                10,
                null,
                null,
                "https://example.com/image.jpg",
                "https://example.com/thumb.jpg",
                null,
                Guid.Empty,
                null);
        act.Should().Throw<ArgumentException>();
    }

    #region GameDesigner Tests

    [Fact]
    public void GameDesigner_Create_WithValidName_CreatesSuccessfully()
    {
        // Arrange & Act
        var designer = GameDesigner.Create("Reiner Knizia");

        // Assert
        designer.Id.Should().NotBe(Guid.Empty);
        designer.Name.Should().Be("Reiner Knizia");
        (designer.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GameDesigner_Create_WithInvalidName_ThrowsArgumentException(string invalidName)
    {
        // Act & Assert
        var act = () => GameDesigner.Create(invalidName);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GameDesigner_Create_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var longName = new string('A', 201);

        // Act & Assert
        var act = () => GameDesigner.Create(longName);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region GamePublisher Tests

    [Fact]
    public void GamePublisher_Create_WithValidName_CreatesSuccessfully()
    {
        // Arrange & Act
        var publisher = GamePublisher.Create("CMON");

        // Assert
        publisher.Id.Should().NotBe(Guid.Empty);
        publisher.Name.Should().Be("CMON");
        (publisher.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GamePublisher_Create_WithInvalidName_ThrowsArgumentException(string invalidName)
    {
        // Act & Assert
        var act = () => GamePublisher.Create(invalidName);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GamePublisher_Create_WithNameTooLong_ThrowsArgumentException()
    {
        // Arrange
        var longName = new string('A', 201);

        // Act & Assert
        var act = () => GamePublisher.Create(longName);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    // ========================================
    // APPROVAL WORKFLOW TESTS (Issue #2514)
    // ========================================

    [Fact]
    public void SubmitForApproval_WithDraftStatus_TransitionsToPendingApproval()
    {
        // Arrange
        var game = CreateValidGame();
        var submitterId = Guid.NewGuid();
        game.Status.Should().Be(GameStatus.Draft);

        // Act
        game.SubmitForApproval(submitterId);

        // Assert
        game.Status.Should().Be(GameStatus.PendingApproval);
        game.ModifiedBy.Should().Be(submitterId);
        game.ModifiedAt.Should().NotBeNull();

        // Assert domain event raised
        var submitEvent = game.DomainEvents.OfType<SharedGameSubmittedForApprovalEvent>().LastOrDefault();
        submitEvent.Should().NotBeNull();
        submitEvent.GameId.Should().Be(game.Id);
        submitEvent.SubmittedBy.Should().Be(submitterId);
    }

    [Fact]
    public void SubmitForApproval_WithNonDraftStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(TestUserId);
        game.ApprovePublication(TestUserId); // Move to Published

        // Act & Assert
        var act = () =>
            game.SubmitForApproval(Guid.NewGuid());
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Only Draft games can be submitted");
    }

    [Fact]
    public void ApprovePublication_WithPendingApprovalStatus_TransitionsToPublished()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(TestUserId);
        game.Status.Should().Be(GameStatus.PendingApproval);

        var approverId = Guid.NewGuid();

        // Act
        game.ApprovePublication(approverId);

        // Assert
        game.Status.Should().Be(GameStatus.Published);
        game.ModifiedBy.Should().Be(approverId);
        game.ModifiedAt.Should().NotBeNull();

        // Assert domain event raised
        var approveEvent = game.DomainEvents.OfType<SharedGamePublicationApprovedEvent>().LastOrDefault();
        approveEvent.Should().NotBeNull();
        approveEvent.GameId.Should().Be(game.Id);
        approveEvent.ApprovedBy.Should().Be(approverId);
    }

    [Fact]
    public void ApprovePublication_WithNonPendingApprovalStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        game.Status.Should().Be(GameStatus.Draft);

        // Act & Assert
        var act = () =>
            game.ApprovePublication(Guid.NewGuid());
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Only PendingApproval games can be approved");
    }

    [Fact]
    public void RejectPublication_WithPendingApprovalStatus_TransitionsToDraft()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(TestUserId);
        game.Status.Should().Be(GameStatus.PendingApproval);

        var rejecterId = Guid.NewGuid();
        var reason = "Needs more information about player count";

        // Act
        game.RejectPublication(rejecterId, reason);

        // Assert
        game.Status.Should().Be(GameStatus.Draft);
        game.ModifiedBy.Should().Be(rejecterId);
        game.ModifiedAt.Should().NotBeNull();

        // Assert domain event raised
        var rejectEvent = game.DomainEvents.OfType<SharedGamePublicationRejectedEvent>().LastOrDefault();
        rejectEvent.Should().NotBeNull();
        rejectEvent.GameId.Should().Be(game.Id);
        rejectEvent.RejectedBy.Should().Be(rejecterId);
        rejectEvent.Reason.Should().Be(reason);
    }

    [Fact]
    public void RejectPublication_WithNonPendingApprovalStatus_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateValidGame();
        game.Status.Should().Be(GameStatus.Draft);

        // Act & Assert
        var act = () =>
            game.RejectPublication(Guid.NewGuid(), "Invalid reason");
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().Contain("Only PendingApproval games can be rejected");
    }

    [Fact]
    public void RejectPublication_WithEmptyReason_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateValidGame();
        game.SubmitForApproval(TestUserId);

        // Act & Assert
        var act = () =>
            game.RejectPublication(Guid.NewGuid(), "");
        var exception = act.Should().Throw<ArgumentException>().Which;
        exception.Message.Should().Contain("Rejection reason is required");
    }

    [Fact]
    public void WorkflowStateTransitions_FollowCorrectSequence()
    {
        // Arrange
        var game = CreateValidGame();
        var submitterId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        // Act & Assert - Complete workflow
        game.Status.Should().Be(GameStatus.Draft);

        game.SubmitForApproval(submitterId);
        game.Status.Should().Be(GameStatus.PendingApproval);

        game.ApprovePublication(approverId);
        game.Status.Should().Be(GameStatus.Published);

        // Verify all events were raised in correct order
        var events = game.DomainEvents.ToList();
        events.Should().Contain(e => e is SharedGameCreatedEvent);
        events.Should().Contain(e => e is SharedGameSubmittedForApprovalEvent);
        events.Should().Contain(e => e is SharedGamePublicationApprovedEvent);
    }

    private static SharedGame CreateValidGame()
    {
        return SharedGame.Create(
            "Test Game",
            2020,
            "Test description",
            2,
            4,
            60,
            8,
            2.0m,
            7.0m,
            "https://example.com/test.jpg",
            "https://example.com/test-thumb.jpg",
            GameRules.Create("Test rules", "en"),
            TestUserId,
            null);
    }
}
