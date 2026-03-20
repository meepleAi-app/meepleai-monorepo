using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the GameMechanic entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 6
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameMechanicTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsMechanicWithNewId()
    {
        // Arrange
        var name = "Worker Placement";
        var slug = "worker-placement";

        // Act
        var mechanic = GameMechanic.Create(name, slug);

        // Assert
        mechanic.Should().NotBeNull();
        mechanic.Id.Should().NotBe(Guid.Empty);
        mechanic.Name.Should().Be(name);
        mechanic.Slug.Should().Be(slug);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Act
        var mechanic1 = GameMechanic.Create("Deck Building", "deck-building");
        var mechanic2 = GameMechanic.Create("Dice Rolling", "dice-rolling");

        // Assert
        mechanic1.Id.Should().NotBe(mechanic2.Id);
    }

    [Fact]
    public void Create_WithMaxLengthName_Succeeds()
    {
        // Arrange
        var name = new string('A', 100);
        var slug = "test-slug";

        // Act
        var mechanic = GameMechanic.Create(name, slug);

        // Assert
        mechanic.Name.Should().HaveLength(100);
    }

    [Fact]
    public void Create_WithMaxLengthSlug_Succeeds()
    {
        // Arrange
        var name = "Test Mechanic";
        var slug = new string('a', 100);

        // Act
        var mechanic = GameMechanic.Create(name, slug);

        // Assert
        mechanic.Slug.Should().HaveLength(100);
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
        var action = () => GameMechanic.Create(name!, "valid-slug");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*name*required*");
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var name = new string('A', 101);

        // Act
        var action = () => GameMechanic.Create(name, "valid-slug");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*name*cannot exceed 100 characters*");
    }

    #endregion

    #region Slug Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidSlug_ThrowsArgumentException(string? slug)
    {
        // Act
        var action = () => GameMechanic.Create("Valid Name", slug!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*slug*required*");
    }

    [Fact]
    public void Create_WithSlugExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var slug = new string('a', 101);

        // Act
        var action = () => GameMechanic.Create("Valid Name", slug);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*slug*cannot exceed 100 characters*");
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_ReconstitutesFromPersistence()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "Area Control";
        var slug = "area-control";

        // Act
        var mechanic = new GameMechanic(id, name, slug);

        // Assert
        mechanic.Id.Should().Be(id);
        mechanic.Name.Should().Be(name);
        mechanic.Slug.Should().Be(slug);
    }

    #endregion
}
