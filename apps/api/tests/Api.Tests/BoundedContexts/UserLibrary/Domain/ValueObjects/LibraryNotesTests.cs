using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Tests for the LibraryNotes value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 8
/// </summary>
[Trait("Category", "Unit")]
public sealed class LibraryNotesTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidNotes_CreatesInstance()
    {
        // Arrange
        var notes = "Great game for family night!";

        // Act
        var libraryNotes = new LibraryNotes(notes);

        // Assert
        libraryNotes.Value.Should().Be(notes);
    }

    [Fact]
    public void Constructor_TrimsWhitespace()
    {
        // Arrange
        var notes = "  Notes with whitespace  ";

        // Act
        var libraryNotes = new LibraryNotes(notes);

        // Assert
        libraryNotes.Value.Should().Be("Notes with whitespace");
    }

    [Fact]
    public void Constructor_WithMaxLength_Succeeds()
    {
        // Arrange - 500 characters
        var notes = new string('A', 500);

        // Act
        var libraryNotes = new LibraryNotes(notes);

        // Assert
        libraryNotes.Value.Should().HaveLength(500);
    }

    #endregion

    #region Validation Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespace_ThrowsArgumentException(string? notes)
    {
        // Act
        var action = () => new LibraryNotes(notes!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Notes cannot be empty or whitespace*");
    }

    [Fact]
    public void Constructor_WithExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange - 501 characters
        var notes = new string('A', 501);

        // Act
        var action = () => new LibraryNotes(notes);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Notes cannot exceed 500 characters*");
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
    public void FromNullable_WithEmptyOrWhitespace_ReturnsNull(string? notes)
    {
        // Act
        var result = LibraryNotes.FromNullable(notes);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region ToString and Implicit Conversion Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var notes = "My personal notes";
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
        var notes = "My personal notes";
        var libraryNotes = new LibraryNotes(notes);

        // Act
        string value = libraryNotes;

        // Assert
        value.Should().Be(notes);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void TwoNotes_WithSameValue_AreEqual()
    {
        // Arrange
        var notes1 = new LibraryNotes("Same content");
        var notes2 = new LibraryNotes("Same content");

        // Assert
        notes1.Should().Be(notes2);
        notes1.Equals(notes2).Should().BeTrue();
    }

    [Fact]
    public void TwoNotes_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var notes1 = new LibraryNotes("Content A");
        var notes2 = new LibraryNotes("Content B");

        // Assert
        notes1.Should().NotBe(notes2);
    }

    [Fact]
    public void TwoNotes_WithSameValue_HaveSameHashCode()
    {
        // Arrange
        var notes1 = new LibraryNotes("Same content");
        var notes2 = new LibraryNotes("Same content");

        // Assert
        notes1.GetHashCode().Should().Be(notes2.GetHashCode());
    }

    #endregion
}
