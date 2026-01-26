using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Tests for the LibraryNotes value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class LibraryNotesTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidNotes_CreatesInstance()
    {
        // Arrange
        var notes = "Great game for family nights!";

        // Act
        var libraryNotes = new LibraryNotes(notes);

        // Assert
        libraryNotes.Value.Should().Be(notes);
    }

    [Fact]
    public void Constructor_WithWhitespace_TrimsValue()
    {
        // Arrange
        var notes = "  Notes with whitespace  ";

        // Act
        var libraryNotes = new LibraryNotes(notes);

        // Assert
        libraryNotes.Value.Should().Be("Notes with whitespace");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyOrNull_ThrowsArgumentException(string? notes)
    {
        // Act
        var action = () => new LibraryNotes(notes!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Notes cannot be empty or whitespace*");
    }

    [Fact]
    public void Constructor_WithMaxLength_Succeeds()
    {
        // Arrange - 500 characters
        var notes = new string('x', 500);

        // Act
        var libraryNotes = new LibraryNotes(notes);

        // Assert
        libraryNotes.Value.Should().HaveLength(500);
    }

    [Fact]
    public void Constructor_ExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange - 501 characters
        var notes = new string('x', 501);

        // Act
        var action = () => new LibraryNotes(notes);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Notes cannot exceed 500 characters*");
    }

    #endregion

    #region FromNullable Tests

    [Fact]
    public void FromNullable_WithValidNotes_ReturnsInstance()
    {
        // Arrange
        var notes = "Valid notes";

        // Act
        var result = LibraryNotes.FromNullable(notes);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be(notes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromNullable_WithEmptyOrNull_ReturnsNull(string? notes)
    {
        // Act
        var result = LibraryNotes.FromNullable(notes);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValue_ReturnsTrue()
    {
        // Arrange
        var notes1 = new LibraryNotes("Same notes");
        var notes2 = new LibraryNotes("Same notes");

        // Act & Assert
        notes1.Should().Be(notes2);
    }

    [Fact]
    public void Equals_WithDifferentValue_ReturnsFalse()
    {
        // Arrange
        var notes1 = new LibraryNotes("First notes");
        var notes2 = new LibraryNotes("Second notes");

        // Act & Assert
        notes1.Should().NotBe(notes2);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        // Arrange
        var notes1 = new LibraryNotes("Same notes");
        var notes2 = new LibraryNotes("Same notes");

        // Act & Assert
        notes1.GetHashCode().Should().Be(notes2.GetHashCode());
    }

    #endregion

    #region Conversion Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var notes = "My notes";
        var libraryNotes = new LibraryNotes(notes);

        // Act
        var result = libraryNotes.ToString();

        // Assert
        result.Should().Be(notes);
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var libraryNotes = new LibraryNotes("Test notes");

        // Act
        string value = libraryNotes;

        // Assert
        value.Should().Be("Test notes");
    }

    #endregion
}
