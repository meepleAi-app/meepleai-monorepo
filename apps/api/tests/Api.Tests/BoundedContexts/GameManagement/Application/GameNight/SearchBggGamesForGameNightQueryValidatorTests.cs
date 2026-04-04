using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.BoundedContexts.GameManagement.Application.Validators.GameNight;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for SearchBggGamesForGameNightQueryValidator.
/// Validates all input constraints for the Game Night BGG search.
/// Game Night Improvvisata - E1-1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SearchBggGamesForGameNightQueryValidatorTests
{
    private readonly SearchBggGamesForGameNightQueryValidator _validator;

    public SearchBggGamesForGameNightQueryValidatorTests()
    {
        _validator = new SearchBggGamesForGameNightQueryValidator();
    }

    [Fact]
    public async Task Validate_WithValidQuery_PassesValidation()
    {
        // Arrange
        var query = new SearchBggGamesForGameNightQuery("Gloomhaven", Page: 1, PageSize: 20);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_WithEmptySearchTerm_FailsValidation(string searchTerm)
    {
        // Arrange
        var query = new SearchBggGamesForGameNightQuery(searchTerm);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(query.SearchTerm));
    }

    [Fact]
    public async Task Validate_WithSearchTermTooShort_FailsValidation()
    {
        // Arrange
        var query = new SearchBggGamesForGameNightQuery("A"); // Only 1 character

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(query.SearchTerm) &&
            e.ErrorMessage.Contains("2 characters"));
    }

    [Fact]
    public async Task Validate_WithSearchTermTooLong_FailsValidation()
    {
        // Arrange
        var longTerm = new string('a', 201);
        var query = new SearchBggGamesForGameNightQuery(longTerm);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(query.SearchTerm) &&
            e.ErrorMessage.Contains("200 characters"));
    }

    [Fact]
    public async Task Validate_WithSearchTermExactlyMinLength_PassesValidation()
    {
        // Arrange
        var query = new SearchBggGamesForGameNightQuery("Go"); // Exactly 2 characters

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithPageLessThanOne_FailsValidation()
    {
        // Arrange
        var query = new SearchBggGamesForGameNightQuery("Catan", Page: 0);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(query.Page));
    }

    [Fact]
    public async Task Validate_WithPageSizeTooLarge_FailsValidation()
    {
        // Arrange
        var query = new SearchBggGamesForGameNightQuery("Catan", PageSize: 51);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(query.PageSize));
    }

    [Fact]
    public async Task Validate_WithPageSizeZero_FailsValidation()
    {
        // Arrange
        var query = new SearchBggGamesForGameNightQuery("Catan", PageSize: 0);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(query.PageSize));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(25)]
    [InlineData(50)]
    public async Task Validate_WithValidPageSize_PassesValidation(int pageSize)
    {
        // Arrange
        var query = new SearchBggGamesForGameNightQuery("Catan", PageSize: pageSize);

        // Act
        var result = await _validator.ValidateAsync(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
