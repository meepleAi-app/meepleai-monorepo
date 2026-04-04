using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class CreateSharedGameCommandValidatorTests
{
    private readonly CreateSharedGameCommandValidator _validator;

    public CreateSharedGameCommandValidatorTests()
    {
        _validator = new CreateSharedGameCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            "Catan", 1995, "Trade and build on the island of Catan", 3, 4, 90, 10,
            2.5m, 7.5m, "https://example.com/image.jpg", "https://example.com/thumb.jpg",
            null, Guid.NewGuid(), null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyTitle_FailsValidation(string title)
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            title, 1995, "Description", 3, 4, 90, 10,
            null, null, "https://img.jpg", "https://thumb.jpg", null, Guid.NewGuid(), null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Theory]
    [InlineData(1800)]
    [InlineData(2200)]
    public void Validate_WithInvalidYear_FailsValidation(int year)
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            "Catan", year, "Description", 3, 4, 90, 10,
            null, null, "https://img.jpg", "https://thumb.jpg", null, Guid.NewGuid(), null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.YearPublished);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidMinPlayers_FailsValidation(int minPlayers)
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            "Catan", 1995, "Description", minPlayers, 4, 90, 10,
            null, null, "https://img.jpg", "https://thumb.jpg", null, Guid.NewGuid(), null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MinPlayers);
    }

    [Fact]
    public void Validate_WithMaxPlayersLessThanMin_FailsValidation()
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            "Catan", 1995, "Description", 5, 2, 90, 10,
            null, null, "https://img.jpg", "https://thumb.jpg", null, Guid.NewGuid(), null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.MaxPlayers);
    }

    [Theory]
    [InlineData("invalid-url")]
    [InlineData("")]
    public void Validate_WithInvalidImageUrl_FailsValidation(string imageUrl)
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            "Catan", 1995, "Description", 3, 4, 90, 10,
            null, null, imageUrl, "https://thumb.jpg", null, Guid.NewGuid(), null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ImageUrl);
    }

    [Fact]
    public void Validate_WithEmptyCreatedBy_FailsValidation()
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            "Catan", 1995, "Description", 3, 4, 90, 10,
            null, null, "https://img.jpg", "https://thumb.jpg", null, Guid.Empty, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CreatedBy);
    }

    // ── Metadata list field tests ────────────────────────────────────────────

    private static CreateSharedGameCommand CreateValidCommand() =>
        new("Catan", 1995, "Trade and build", 3, 4, 90, 10,
            2.5m, 7.5m, "https://example.com/image.jpg", "https://example.com/thumb.jpg",
            null, Guid.NewGuid(), null);

    [Fact]
    public void Should_Pass_When_Categories_Is_Null()
    {
        var command = CreateValidCommand() with { Categories = null };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Pass_When_Categories_Has_Valid_Items()
    {
        var command = CreateValidCommand() with { Categories = ["Strategy", "Euro"] };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_Categories_Exceeds_Max_Items()
    {
        var categories = Enumerable.Range(1, 51).Select(i => $"Category{i}").ToList();
        var command = CreateValidCommand() with { Categories = categories };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Categories);
    }

    [Fact]
    public void Should_Fail_When_Categories_Contains_Empty_String()
    {
        var command = CreateValidCommand() with { Categories = ["Strategy", ""] };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("Categories[1]");
    }

    [Fact]
    public void Should_Fail_When_Mechanics_Exceeds_Max_Items()
    {
        var mechanics = Enumerable.Range(1, 51).Select(i => $"Mechanic{i}").ToList();
        var command = CreateValidCommand() with { Mechanics = mechanics };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Mechanics);
    }

    [Fact]
    public void Should_Fail_When_Designers_Exceeds_Max_Items()
    {
        var designers = Enumerable.Range(1, 21).Select(i => $"Designer{i}").ToList();
        var command = CreateValidCommand() with { Designers = designers };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Designers);
    }

    [Fact]
    public void Should_Fail_When_Publishers_Exceeds_Max_Items()
    {
        var publishers = Enumerable.Range(1, 21).Select(i => $"Publisher{i}").ToList();
        var command = CreateValidCommand() with { Publishers = publishers };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Publishers);
    }
}
