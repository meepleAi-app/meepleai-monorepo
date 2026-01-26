using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the GamePublisher entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 6
/// </summary>
[Trait("Category", "Unit")]
public sealed class GamePublisherTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidName_ReturnsPublisherWithNewId()
    {
        // Arrange
        var name = "Fantasy Flight Games";

        // Act
        var publisher = GamePublisher.Create(name);

        // Assert
        publisher.Should().NotBeNull();
        publisher.Id.Should().NotBe(Guid.Empty);
        publisher.Name.Should().Be(name);
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var publisher = GamePublisher.Create("Stonemaier Games");
        var after = DateTime.UtcNow;

        // Assert
        publisher.CreatedAt.Should().BeOnOrAfter(before);
        publisher.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Act
        var publisher1 = GamePublisher.Create("Publisher 1");
        var publisher2 = GamePublisher.Create("Publisher 2");

        // Assert
        publisher1.Id.Should().NotBe(publisher2.Id);
    }

    [Fact]
    public void Create_WithMaxLengthName_Succeeds()
    {
        // Arrange
        var name = new string('A', 200);

        // Act
        var publisher = GamePublisher.Create(name);

        // Assert
        publisher.Name.Should().HaveLength(200);
    }

    #endregion

    #region Name Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string? name)
    {
        // Act
        var action = () => GamePublisher.Create(name!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*name*required*");
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var name = new string('A', 201);

        // Act
        var action = () => GamePublisher.Create(name);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*name*cannot exceed 200 characters*");
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_ReconstitutesFromPersistence()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Asmodee";
        var createdAt = new DateTime(2024, 6, 20, 14, 0, 0, DateTimeKind.Utc);

        // Act
        var publisher = new GamePublisher(id, name, createdAt);

        // Assert
        publisher.Id.Should().Be(id);
        publisher.Name.Should().Be(name);
        publisher.CreatedAt.Should().Be(createdAt);
    }

    #endregion
}
