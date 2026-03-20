using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Validators;

/// <summary>
/// Unit tests for GetFilteredSharedGamesQueryValidator.
/// Issue #3533: Admin API Endpoints - Filtered Games List Validation
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GetFilteredSharedGamesQueryValidatorTests
{
    private readonly GetFilteredSharedGamesQueryValidator _validator = new();

    #region PageNumber Validation

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Validate_WithValidPageNumber_Passes(int pageNumber)
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(PageNumber: pageNumber);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithInvalidPageNumber_Fails(int pageNumber)
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(PageNumber: pageNumber);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageNumber");
    }

    #endregion

    #region PageSize Validation

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    [InlineData(100)]
    public void Validate_WithValidPageSize_Passes(int pageSize)
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(PageSize: pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_WithInvalidPageSize_Fails(int pageSize)
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(PageSize: pageSize);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PageSize");
    }

    #endregion

    #region Search Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("valid search")]
    [InlineData("catan")]
    public void Validate_WithValidSearch_Passes(string? search)
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(Search: search);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithTooLongSearch_Fails()
    {
        // Arrange
        var longSearch = new string('a', 201);
        var query = new GetFilteredSharedGamesQuery(Search: longSearch);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Search");
    }

    [Fact]
    public void Validate_WithMaxLengthSearch_Passes()
    {
        // Arrange
        var maxSearch = new string('a', 200);
        var query = new GetFilteredSharedGamesQuery(Search: maxSearch);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region SortBy Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("title")]
    [InlineData("title:asc")]
    [InlineData("title:desc")]
    [InlineData("createdAt")]
    [InlineData("createdAt:asc")]
    [InlineData("modifiedAt:desc")]
    [InlineData("status")]
    [InlineData("yearPublished:asc")]
    public void Validate_WithValidSortBy_Passes(string? sortBy)
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(SortBy: sortBy);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("invalidField")]
    [InlineData("name")]
    [InlineData("title:invalid")]
    [InlineData("title:ascending")]
    public void Validate_WithInvalidSortBy_Fails(string sortBy)
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(SortBy: sortBy);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SortBy");
    }

    #endregion

    #region Status Filter Validation

    [Theory]
    [InlineData(null)]
    [InlineData(GameStatus.Draft)]
    [InlineData(GameStatus.Published)]
    [InlineData(GameStatus.PendingApproval)]
    [InlineData(GameStatus.Archived)]
    public void Validate_WithValidStatus_Passes(GameStatus? status)
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(Status: status);

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Combined Validation

    [Fact]
    public void Validate_WithAllValidParameters_Passes()
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(
            Status: GameStatus.Published,
            Search: "catan",
            PageNumber: 1,
            PageSize: 20,
            SortBy: "title:asc",
            SubmittedBy: Guid.NewGuid());

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithDefaultValues_Passes()
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery();

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReportsAll()
    {
        // Arrange
        var query = new GetFilteredSharedGamesQuery(
            PageNumber: 0,
            PageSize: 0,
            Search: new string('a', 201),
            SortBy: "invalid");

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    #endregion
}
