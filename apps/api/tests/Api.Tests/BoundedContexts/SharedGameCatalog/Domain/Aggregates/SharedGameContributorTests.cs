using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

/// <summary>
/// Unit tests for SharedGame contributor-related methods.
/// Tests Issue #2720: Contributor Entity e Sistema Attribuzioni
/// </summary>
public class SharedGameContributorTests
{
    #region AddContributor Tests

    [Fact]
    public void AddContributor_WithValidParameters_AddsContributor()
    {
        // Arrange
        var game = CreateTestSharedGame();
        var userId = Guid.NewGuid();

        // Act
        var contributor = game.AddContributor(userId, isPrimary: false);

        // Assert
        contributor.Should().NotBeNull();
        contributor.UserId.Should().Be(userId);
        contributor.SharedGameId.Should().Be(game.Id);
        contributor.IsPrimaryContributor.Should().BeFalse();
        game.Contributors.Should().Contain(contributor);
        game.ContributorCount.Should().Be(1);
    }

    [Fact]
    public void AddContributor_AsPrimary_AddsAsPrimaryContributor()
    {
        // Arrange
        var game = CreateTestSharedGame();
        var userId = Guid.NewGuid();

        // Act
        var contributor = game.AddContributor(userId, isPrimary: true);

        // Assert
        contributor.IsPrimaryContributor.Should().BeTrue();
        game.GetPrimaryContributor().Should().Be(contributor);
    }

    [Fact]
    public void AddContributor_RaisesContributorAddedEvent()
    {
        // Arrange
        var game = CreateTestSharedGame();
        game.ClearDomainEvents(); // Clear creation event
        var userId = Guid.NewGuid();

        // Act
        var contributor = game.AddContributor(userId, isPrimary: true);

        // Assert
        var domainEvents = game.DomainEvents.ToList();
        domainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ContributorAddedEvent>();

        var evt = (ContributorAddedEvent)domainEvents.First();
        evt.ContributorId.Should().Be(contributor.Id);
        evt.UserId.Should().Be(userId);
        evt.SharedGameId.Should().Be(game.Id);
        evt.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void AddContributor_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateTestSharedGame();

        // Act
        var act = () => game.AddContributor(Guid.Empty, isPrimary: false);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void AddContributor_WhenUserAlreadyContributor_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateTestSharedGame();
        var userId = Guid.NewGuid();
        game.AddContributor(userId, isPrimary: false);

        // Act
        var act = () => game.AddContributor(userId, isPrimary: false);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*User {userId} is already a contributor*");
    }

    [Fact]
    public void AddContributor_WhenPrimaryAlreadyExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateTestSharedGame();
        game.AddContributor(Guid.NewGuid(), isPrimary: true);

        // Act
        var act = () => game.AddContributor(Guid.NewGuid(), isPrimary: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*primary contributor already exists*");
    }

    [Fact]
    public void AddContributor_MultipleNonPrimary_Succeeds()
    {
        // Arrange
        var game = CreateTestSharedGame();

        // Act
        var contributor1 = game.AddContributor(Guid.NewGuid(), isPrimary: false);
        var contributor2 = game.AddContributor(Guid.NewGuid(), isPrimary: false);
        var contributor3 = game.AddContributor(Guid.NewGuid(), isPrimary: false);

        // Assert
        game.ContributorCount.Should().Be(3);
        game.Contributors.Should().Contain(new[] { contributor1, contributor2, contributor3 });
    }

    #endregion

    #region AddPrimaryContributorWithInitialSubmission Tests

    [Fact]
    public void AddPrimaryContributorWithInitialSubmission_CreatesContributorWithContribution()
    {
        // Arrange
        var game = CreateTestSharedGame();
        var userId = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var contributor = game.AddPrimaryContributorWithInitialSubmission(
            userId,
            shareRequestId,
            documentIds);

        // Assert
        contributor.Should().NotBeNull();
        contributor.IsPrimaryContributor.Should().BeTrue();
        contributor.ContributionCount.Should().Be(1);

        var contribution = contributor.Contributions.First();
        contribution.Type.Should().Be(ContributionRecordType.InitialSubmission);
        contribution.ShareRequestId.Should().Be(shareRequestId);
        contribution.DocumentIds.Should().BeEquivalentTo(documentIds);
    }

    [Fact]
    public void AddPrimaryContributorWithInitialSubmission_RaisesEvent()
    {
        // Arrange
        var game = CreateTestSharedGame();
        game.ClearDomainEvents();
        var userId = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();

        // Act
        var contributor = game.AddPrimaryContributorWithInitialSubmission(
            userId,
            shareRequestId);

        // Assert
        var domainEvents = game.DomainEvents.ToList();
        domainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ContributorAddedEvent>();

        var evt = (ContributorAddedEvent)domainEvents.First();
        evt.IsPrimary.Should().BeTrue();
    }

    [Fact]
    public void AddPrimaryContributorWithInitialSubmission_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateTestSharedGame();

        // Act
        var act = () => game.AddPrimaryContributorWithInitialSubmission(
            Guid.Empty,
            Guid.NewGuid());

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void AddPrimaryContributorWithInitialSubmission_WithEmptyShareRequestId_ThrowsArgumentException()
    {
        // Arrange
        var game = CreateTestSharedGame();

        // Act
        var act = () => game.AddPrimaryContributorWithInitialSubmission(
            Guid.NewGuid(),
            Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("shareRequestId")
            .WithMessage("*ShareRequestId cannot be empty*");
    }

    [Fact]
    public void AddPrimaryContributorWithInitialSubmission_WhenPrimaryExists_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateTestSharedGame();
        game.AddContributor(Guid.NewGuid(), isPrimary: true);

        // Act
        var act = () => game.AddPrimaryContributorWithInitialSubmission(
            Guid.NewGuid(),
            Guid.NewGuid());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*primary contributor already exists*");
    }

    #endregion

    #region GetPrimaryContributor Tests

    [Fact]
    public void GetPrimaryContributor_WhenNoPrimary_ReturnsNull()
    {
        // Arrange
        var game = CreateTestSharedGame();
        game.AddContributor(Guid.NewGuid(), isPrimary: false);

        // Act
        var primary = game.GetPrimaryContributor();

        // Assert
        primary.Should().BeNull();
    }

    [Fact]
    public void GetPrimaryContributor_WhenPrimaryExists_ReturnsPrimary()
    {
        // Arrange
        var game = CreateTestSharedGame();
        var primaryUserId = Guid.NewGuid();
        var primaryContributor = game.AddContributor(primaryUserId, isPrimary: true);
        game.AddContributor(Guid.NewGuid(), isPrimary: false);

        // Act
        var primary = game.GetPrimaryContributor();

        // Assert
        primary.Should().Be(primaryContributor);
        primary!.UserId.Should().Be(primaryUserId);
    }

    #endregion

    #region GetAllContributors Tests

    [Fact]
    public void GetAllContributors_WhenNoContributors_ReturnsEmpty()
    {
        // Arrange
        var game = CreateTestSharedGame();

        // Act
        var contributors = game.GetAllContributors();

        // Assert
        contributors.Should().BeEmpty();
    }

    [Fact]
    public void GetAllContributors_ReturnsAllContributors()
    {
        // Arrange
        var game = CreateTestSharedGame();
        var contributor1 = game.AddContributor(Guid.NewGuid(), isPrimary: true);
        var contributor2 = game.AddContributor(Guid.NewGuid(), isPrimary: false);
        var contributor3 = game.AddContributor(Guid.NewGuid(), isPrimary: false);

        // Act
        var contributors = game.GetAllContributors().ToList();

        // Assert
        contributors.Should().HaveCount(3);
        contributors.Should().Contain(new[] { contributor1, contributor2, contributor3 });
    }

    #endregion

    #region GetContributorByUserId Tests

    [Fact]
    public void GetContributorByUserId_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var game = CreateTestSharedGame();
        game.AddContributor(Guid.NewGuid(), isPrimary: false);

        // Act
        var contributor = game.GetContributorByUserId(Guid.NewGuid());

        // Assert
        contributor.Should().BeNull();
    }

    [Fact]
    public void GetContributorByUserId_WhenFound_ReturnsContributor()
    {
        // Arrange
        var game = CreateTestSharedGame();
        var userId = Guid.NewGuid();
        var expectedContributor = game.AddContributor(userId, isPrimary: false);

        // Act
        var contributor = game.GetContributorByUserId(userId);

        // Assert
        contributor.Should().Be(expectedContributor);
    }

    #endregion

    #region ContributorCount Tests

    [Fact]
    public void ContributorCount_ReflectsNumberOfContributors()
    {
        // Arrange
        var game = CreateTestSharedGame();

        // Assert initial
        game.ContributorCount.Should().Be(0);

        // Add contributors and check count
        game.AddContributor(Guid.NewGuid(), isPrimary: true);
        game.ContributorCount.Should().Be(1);

        game.AddContributor(Guid.NewGuid(), isPrimary: false);
        game.ContributorCount.Should().Be(2);

        game.AddContributor(Guid.NewGuid(), isPrimary: false);
        game.ContributorCount.Should().Be(3);
    }

    #endregion

    #region IsContributor Tests

    [Fact]
    public void IsContributor_WhenNotContributor_ReturnsFalse()
    {
        // Arrange
        var game = CreateTestSharedGame();
        game.AddContributor(Guid.NewGuid(), isPrimary: false);

        // Act
        var result = game.IsContributor(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsContributor_WhenIsContributor_ReturnsTrue()
    {
        // Arrange
        var game = CreateTestSharedGame();
        var userId = Guid.NewGuid();
        game.AddContributor(userId, isPrimary: false);

        // Act
        var result = game.IsContributor(userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsContributor_WhenNoContributors_ReturnsFalse()
    {
        // Arrange
        var game = CreateTestSharedGame();

        // Act
        var result = game.IsContributor(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void CompleteContributorWorkflow_IntegrationTest()
    {
        // Arrange
        var game = CreateTestSharedGame();
        var primaryUserId = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid() };

        // Act - Add primary contributor with initial submission
        var primaryContributor = game.AddPrimaryContributorWithInitialSubmission(
            primaryUserId,
            shareRequestId,
            documentIds);

        // Record additional contributions from primary
        primaryContributor.RecordDocumentAddition(
            new List<Guid> { Guid.NewGuid() },
            "Added rulebook");

        // Add secondary contributor
        var secondaryUserId = Guid.NewGuid();
        var secondaryContributor = game.AddContributor(secondaryUserId, isPrimary: false);

        // Record contribution from secondary
        secondaryContributor.RecordMetadataUpdate("Updated player count");

        // Assert
        game.ContributorCount.Should().Be(2);
        game.GetPrimaryContributor().Should().Be(primaryContributor);
        game.IsContributor(primaryUserId).Should().BeTrue();
        game.IsContributor(secondaryUserId).Should().BeTrue();

        primaryContributor.ContributionCount.Should().Be(2);
        primaryContributor.LatestVersion.Should().Be(2);

        secondaryContributor.ContributionCount.Should().Be(1);
        secondaryContributor.LatestVersion.Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private static SharedGame CreateTestSharedGame()
    {
        return SharedGame.Create(
            title: "Test Game",
            yearPublished: 2024,
            description: "A test game for unit testing",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 12,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid());
    }

    #endregion
}
