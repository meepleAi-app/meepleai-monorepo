using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Tests for the GameTitle value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 19
/// </summary>
[Trait("Category", "Unit")]
public sealed class GameTitleTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidTitle_CreatesGameTitle()
    {
        // Arrange
        var title = "Catan";

        // Act
        var gameTitle = new GameTitle(title);

        // Assert
        gameTitle.Value.Should().Be("Catan");
    }

    [Fact]
    public void Constructor_WithEmptyString_ThrowsValidationException()
    {
        // Act
        var action = () => new GameTitle("");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Game title cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespace_ThrowsValidationException()
    {
        // Act
        var action = () => new GameTitle("   ");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Game title cannot be empty*");
    }

    [Fact]
    public void Constructor_WithTitleExceeding200Characters_ThrowsValidationException()
    {
        // Arrange
        var longTitle = new string('a', 201);

        // Act
        var action = () => new GameTitle(longTitle);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Game title cannot exceed 200 characters*");
    }

    [Fact]
    public void Constructor_WithTitleExactly200Characters_Succeeds()
    {
        // Arrange
        var maxTitle = new string('a', 200);

        // Act
        var gameTitle = new GameTitle(maxTitle);

        // Assert
        gameTitle.Value.Should().HaveLength(200);
    }

    [Fact]
    public void Constructor_TrimsTitle()
    {
        // Act
        var gameTitle = new GameTitle("  Catan  ");

        // Assert
        gameTitle.Value.Should().Be("Catan");
    }

    #endregion

    #region NormalizedValue Tests

    [Fact]
    public void NormalizedValue_ConvertsToLowercase()
    {
        // Act
        var gameTitle = new GameTitle("CATAN");

        // Assert
        gameTitle.NormalizedValue.Should().Be("catan");
    }

    [Fact]
    public void NormalizedValue_ReplacesMultipleSpacesWithSingle()
    {
        // Act
        var gameTitle = new GameTitle("Ticket  to   Ride");

        // Assert
        gameTitle.NormalizedValue.Should().Be("ticket to ride");
    }

    [Fact]
    public void NormalizedValue_TrimsWhitespace()
    {
        // Act
        var gameTitle = new GameTitle("  Pandemic  ");

        // Assert
        gameTitle.NormalizedValue.Should().Be("pandemic");
    }

    #endregion

    #region Slug Tests

    [Fact]
    public void Slug_ReplacesSpacesWithHyphens()
    {
        // Act
        var gameTitle = new GameTitle("Ticket to Ride");

        // Assert
        gameTitle.Slug.Should().Be("ticket-to-ride");
    }

    [Fact]
    public void Slug_RemovesSpecialCharacters()
    {
        // Act
        var gameTitle = new GameTitle("Agricola: All Creatures Big & Small!");

        // Assert
        gameTitle.Slug.Should().Be("agricola-all-creatures-big-small");
    }

    [Fact]
    public void Slug_RemovesDuplicateHyphens()
    {
        // Act
        var gameTitle = new GameTitle("Catan - The Game");

        // Assert
        gameTitle.Slug.Should().Be("catan-the-game");
    }

    [Fact]
    public void Slug_TrimsHyphensFromEnds()
    {
        // Act
        var gameTitle = new GameTitle("-Catan-");

        // Assert
        gameTitle.Slug.Should().Be("catan");
    }

    [Fact]
    public void Slug_ConvertsToLowercase()
    {
        // Act
        var gameTitle = new GameTitle("AZUL");

        // Assert
        gameTitle.Slug.Should().Be("azul");
    }

    #endregion

    #region GenerateId Tests

    [Fact]
    public void GenerateId_ReturnsDeterministicGuid()
    {
        // Arrange
        var gameTitle1 = new GameTitle("Catan");
        var gameTitle2 = new GameTitle("Catan");

        // Act
        var id1 = gameTitle1.GenerateId();
        var id2 = gameTitle2.GenerateId();

        // Assert
        id1.Should().Be(id2);
    }

    [Fact]
    public void GenerateId_DifferentTitlesProduceDifferentIds()
    {
        // Arrange
        var gameTitle1 = new GameTitle("Catan");
        var gameTitle2 = new GameTitle("Pandemic");

        // Act
        var id1 = gameTitle1.GenerateId();
        var id2 = gameTitle2.GenerateId();

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void GenerateId_SameNormalizedTitleProducesSameId()
    {
        // Arrange - same title with different casing/whitespace
        var gameTitle1 = new GameTitle("Catan");
        var gameTitle2 = new GameTitle("  CATAN  ");

        // Act
        var id1 = gameTitle1.GenerateId();
        var id2 = gameTitle2.GenerateId();

        // Assert
        id1.Should().Be(id2);
    }

    [Fact]
    public void GenerateId_ReturnsValidGuid()
    {
        // Arrange
        var gameTitle = new GameTitle("Wingspan");

        // Act
        var id = gameTitle.GenerateId();

        // Assert
        id.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameNormalizedValue_ReturnsTrue()
    {
        // Arrange
        var gameTitle1 = new GameTitle("Catan");
        var gameTitle2 = new GameTitle("CATAN");

        // Assert
        gameTitle1.Should().Be(gameTitle2);
    }

    [Fact]
    public void Equals_WithDifferentNormalizedValue_ReturnsFalse()
    {
        // Arrange
        var gameTitle1 = new GameTitle("Catan");
        var gameTitle2 = new GameTitle("Pandemic");

        // Assert
        gameTitle1.Should().NotBe(gameTitle2);
    }

    [Fact]
    public void GetHashCode_WithSameNormalizedValue_ReturnsSameHash()
    {
        // Arrange
        var gameTitle1 = new GameTitle("Catan");
        var gameTitle2 = new GameTitle("  catan  ");

        // Assert
        gameTitle1.GetHashCode().Should().Be(gameTitle2.GetHashCode());
    }

    #endregion

    #region ToString and Implicit Conversion Tests

    [Fact]
    public void ToString_ReturnsOriginalValue()
    {
        // Arrange
        var gameTitle = new GameTitle("Catan");

        // Act
        var result = gameTitle.ToString();

        // Assert
        result.Should().Be("Catan");
    }

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var gameTitle = new GameTitle("Catan");

        // Act
        string value = gameTitle;

        // Assert
        value.Should().Be("Catan");
    }

    #endregion
}
