using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the GameDesigner entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 6
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameDesignerTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidName_ReturnsDesignerWithNewId()
    {
        // Arrange
        var name = "Reiner Knizia";

        // Act
        var designer = GameDesigner.Create(name);

        // Assert
        designer.Should().NotBeNull();
        designer.Id.Should().NotBe(Guid.Empty);
        designer.Name.Should().Be(name);
    }

    [Fact]
    public void Create_SetsCreatedAtToNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var designer = GameDesigner.Create("Uwe Rosenberg");
        var after = DateTime.UtcNow;

        // Assert
        designer.CreatedAt.Should().BeOnOrAfter(before);
        designer.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Act
        var designer1 = GameDesigner.Create("Designer 1");
        var designer2 = GameDesigner.Create("Designer 2");

        // Assert
        designer1.Id.Should().NotBe(designer2.Id);
    }

    [Fact]
    public void Create_WithMaxLengthName_Succeeds()
    {
        // Arrange
        var name = new string('A', 200);

        // Act
        var designer = GameDesigner.Create(name);

        // Assert
        designer.Name.Should().HaveLength(200);
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
        var action = () => GameDesigner.Create(name!);

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
        var action = () => GameDesigner.Create(name);

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
        var name = "Vital Lacerda";
        var createdAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var designer = new GameDesigner(id, name, createdAt);

        // Assert
        designer.Id.Should().Be(id);
        designer.Name.Should().Be(name);
        designer.CreatedAt.Should().Be(createdAt);
    }

    #endregion
}