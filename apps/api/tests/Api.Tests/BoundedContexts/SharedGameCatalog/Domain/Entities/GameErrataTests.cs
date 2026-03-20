using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the GameErrata entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 6
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameErrataTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsErrataWithNewId()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var description = "The card effect should read 'Draw 2 cards' instead of 'Draw 3 cards'";
        var pageReference = "Page 15, Card Effects";
        var publishedDate = DateTime.UtcNow.AddDays(-7);

        // Act
        var errata = GameErrata.Create(sharedGameId, description, pageReference, publishedDate);

        // Assert
        errata.Should().NotBeNull();
        errata.Id.Should().NotBe(Guid.Empty);
        errata.SharedGameId.Should().Be(sharedGameId);
        errata.Description.Should().Be(description);
        errata.PageReference.Should().Be(pageReference);
        errata.PublishedDate.Should().Be(publishedDate);
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        // Arrange
        var before = DateTime.UtcNow;
        var sharedGameId = Guid.NewGuid();

        // Act
        var errata = GameErrata.Create(
            sharedGameId,
            "Test description",
            "Page 1",
            DateTime.UtcNow.AddDays(-1));
        var after = DateTime.UtcNow;

        // Assert
        errata.CreatedAt.Should().BeOnOrAfter(before);
        errata.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var publishedDate = DateTime.UtcNow.AddDays(-1);

        // Act
        var errata1 = GameErrata.Create(sharedGameId, "Error 1", "Page 1", publishedDate);
        var errata2 = GameErrata.Create(sharedGameId, "Error 2", "Page 2", publishedDate);

        // Assert
        errata1.Id.Should().NotBe(errata2.Id);
    }

    [Fact]
    public void Create_WithMaxLengthPageReference_Succeeds()
    {
        // Arrange
        var pageReference = new string('P', 100);

        // Act
        var errata = GameErrata.Create(
            Guid.NewGuid(),
            "Test description",
            pageReference,
            DateTime.UtcNow.AddDays(-1));

        // Assert
        errata.PageReference.Should().HaveLength(100);
    }

    #endregion

    #region SharedGameId Validation Tests

    [Fact]
    public void Create_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Act
        var action = () => GameErrata.Create(
            Guid.Empty,
            "Valid description",
            "Page 1",
            DateTime.UtcNow.AddDays(-1));

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SharedGameId*cannot be empty*");
    }

    #endregion

    #region Description Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDescription_ThrowsArgumentException(string? description)
    {
        // Act
        var action = () => GameErrata.Create(
            Guid.NewGuid(),
            description!,
            "Page 1",
            DateTime.UtcNow.AddDays(-1));

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Description*required*");
    }

    #endregion

    #region PageReference Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidPageReference_ThrowsArgumentException(string? pageReference)
    {
        // Act
        var action = () => GameErrata.Create(
            Guid.NewGuid(),
            "Valid description",
            pageReference!,
            DateTime.UtcNow.AddDays(-1));

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*PageReference*required*");
    }

    [Fact]
    public void Create_WithPageReferenceExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var pageReference = new string('P', 101);

        // Act
        var action = () => GameErrata.Create(
            Guid.NewGuid(),
            "Valid description",
            pageReference,
            DateTime.UtcNow.AddDays(-1));

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*PageReference*cannot exceed 100 characters*");
    }

    #endregion

    #region PublishedDate Validation Tests

    [Fact]
    public void Create_WithFuturePublishedDate_ThrowsArgumentException()
    {
        // Act
        var action = () => GameErrata.Create(
            Guid.NewGuid(),
            "Valid description",
            "Page 1",
            DateTime.UtcNow.AddDays(1));

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*PublishedDate*cannot be in the future*");
    }

    [Fact]
    public void Create_WithCurrentPublishedDate_Succeeds()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var errata = GameErrata.Create(
            Guid.NewGuid(),
            "Valid description",
            "Page 1",
            now);

        // Assert
        errata.PublishedDate.Should().Be(now);
    }

    [Fact]
    public void Create_WithPastPublishedDate_Succeeds()
    {
        // Arrange
        var pastDate = DateTime.UtcNow.AddYears(-1);

        // Act
        var errata = GameErrata.Create(
            Guid.NewGuid(),
            "Valid description",
            "Page 1",
            pastDate);

        // Assert
        errata.PublishedDate.Should().Be(pastDate);
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_ReconstitutesFromPersistence()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var description = "Corrected rule clarification";
        var pageReference = "Page 42";
        var publishedDate = new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var createdAt = new DateTime(2024, 3, 16, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var errata = new GameErrata(id, sharedGameId, description, pageReference, publishedDate, createdAt);

        // Assert
        errata.Id.Should().Be(id);
        errata.SharedGameId.Should().Be(sharedGameId);
        errata.Description.Should().Be(description);
        errata.PageReference.Should().Be(pageReference);
        errata.PublishedDate.Should().Be(publishedDate);
        errata.CreatedAt.Should().Be(createdAt);
    }

    #endregion
}