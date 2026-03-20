using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the GameCategory entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 6
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameCategoryTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsCategoryWithNewId()
    {
        // Arrange
        var name = "Strategy";
        var slug = "strategy";

        // Act
        var category = GameCategory.Create(name, slug);

        // Assert
        category.Should().NotBeNull();
        category.Id.Should().NotBe(Guid.Empty);
        category.Name.Should().Be(name);
        category.Slug.Should().Be(slug);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Act
        var category1 = GameCategory.Create("Strategy", "strategy");
        var category2 = GameCategory.Create("Family", "family");

        // Assert
        category1.Id.Should().NotBe(category2.Id);
    }

    [Fact]
    public void Create_WithMaxLengthName_Succeeds()
    {
        // Arrange
        var name = new string('A', 100);
        var slug = "test-slug";

        // Act
        var category = GameCategory.Create(name, slug);

        // Assert
        category.Name.Should().HaveLength(100);
    }

    [Fact]
    public void Create_WithMaxLengthSlug_Succeeds()
    {
        // Arrange
        var name = "Test Category";
        var slug = new string('a', 100);

        // Act
        var category = GameCategory.Create(name, slug);

        // Assert
        category.Slug.Should().HaveLength(100);
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
        var action = () => GameCategory.Create(name!, "valid-slug");

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
        var action = () => GameCategory.Create(name, "valid-slug");

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
        var action = () => GameCategory.Create("Valid Name", slug!);

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
        var action = () => GameCategory.Create("Valid Name", slug);

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
        var name = "Party Games";
        var slug = "party-games";

        // Act - using internal constructor via reflection or factory
        var category = new GameCategory(id, name, slug);

        // Assert
        category.Id.Should().Be(id);
        category.Name.Should().Be(name);
        category.Slug.Should().Be(slug);
    }

    #endregion
}
