using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for <see cref="SearchSharedGamesQueryValidator"/>.
/// Issue #593 (Wave A.3a): the pre-existing query had no validator, so
/// these tests cover the *full* validator surface — pagination, sort
/// whitelist, and numeric ranges (complexity / players / playing time).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SearchSharedGamesQueryValidatorTests
{
    private readonly SearchSharedGamesQueryValidator _validator = new();

    /// <summary>
    /// Builds a query that satisfies every rule by default; individual tests
    /// override only the fields under test.
    /// </summary>
    private static SearchSharedGamesQuery BuildValidQuery(
        int pageNumber = 1,
        int pageSize = 20,
        string sortBy = "Title",
        decimal? minComplexity = null,
        decimal? maxComplexity = null,
        int? minPlayers = null,
        int? maxPlayers = null,
        int? maxPlayingTime = null) =>
        new(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers,
            MaxPlayingTime: maxPlayingTime,
            MinComplexity: minComplexity,
            MaxComplexity: maxComplexity,
            Status: GameStatus.Published,
            PageNumber: pageNumber,
            PageSize: pageSize,
            SortBy: sortBy,
            SortDescending: false);

    // -------------------------------------------------------------------
    // Default / happy path
    // -------------------------------------------------------------------

    [Fact]
    public void Validate_DefaultQuery_Passes()
    {
        // Arrange
        var query = BuildValidQuery();

        // Act
        var result = _validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // PageNumber
    // -------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    public void Validate_PageNumberPositive_Passes(int pageNumber)
    {
        var result = _validator.Validate(BuildValidQuery(pageNumber: pageNumber));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_PageNumberNonPositive_Fails(int pageNumber)
    {
        var result = _validator.Validate(BuildValidQuery(pageNumber: pageNumber));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SearchSharedGamesQuery.PageNumber));
    }

    // -------------------------------------------------------------------
    // PageSize
    // -------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_PageSizeWithinRange_Passes(int pageSize)
    {
        var result = _validator.Validate(BuildValidQuery(pageSize: pageSize));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void Validate_PageSizeOutsideRange_Fails(int pageSize)
    {
        var result = _validator.Validate(BuildValidQuery(pageSize: pageSize));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SearchSharedGamesQuery.PageSize));
    }

    // -------------------------------------------------------------------
    // SortBy whitelist
    // -------------------------------------------------------------------

    [Theory]
    [InlineData("Title")]
    [InlineData("YearPublished")]
    [InlineData("AverageRating")]
    [InlineData("CreatedAt")]
    [InlineData("ComplexityRating")]
    [InlineData("Contrib")] // Issue #593 Commit 1b
    [InlineData("New")]     // Issue #593 Commit 1b
    public void Validate_SortByOnWhitelist_Passes(string sortBy)
    {
        var result = _validator.Validate(BuildValidQuery(sortBy: sortBy));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Random")]
    [InlineData("title")]      // case-sensitive whitelist
    [InlineData("DROP TABLE")]
    [InlineData("")]
    [InlineData("contrib")]    // Issue #593 Commit 1b: case-sensitive — "Contrib" is on whitelist, "contrib" is not
    [InlineData("new")]        // Issue #593 Commit 1b: case-sensitive — "New" is on whitelist, "new" is not
    public void Validate_SortByOffWhitelist_Fails(string sortBy)
    {
        var result = _validator.Validate(BuildValidQuery(sortBy: sortBy));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SearchSharedGamesQuery.SortBy));
    }

    // -------------------------------------------------------------------
    // Complexity (BGG weight 1..5)
    // -------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(2.5)]
    [InlineData(5)]
    public void Validate_MinComplexityInRange_Passes(decimal minComplexity)
    {
        var result = _validator.Validate(BuildValidQuery(minComplexity: minComplexity));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(5.5)]
    [InlineData(100)] // FE bug: passing percent
    public void Validate_MinComplexityOutOfRange_Fails(decimal minComplexity)
    {
        var result = _validator.Validate(BuildValidQuery(minComplexity: minComplexity));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SearchSharedGamesQuery.MinComplexity));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3.7)]
    [InlineData(5)]
    public void Validate_MaxComplexityInRange_Passes(decimal maxComplexity)
    {
        var result = _validator.Validate(BuildValidQuery(maxComplexity: maxComplexity));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5.5)]
    [InlineData(100)]
    public void Validate_MaxComplexityOutOfRange_Fails(decimal maxComplexity)
    {
        var result = _validator.Validate(BuildValidQuery(maxComplexity: maxComplexity));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SearchSharedGamesQuery.MaxComplexity));
    }

    [Fact]
    public void Validate_MaxComplexityLowerThanMinComplexity_Fails()
    {
        var query = BuildValidQuery(minComplexity: 4m, maxComplexity: 2m);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("MaxComplexity must be greater than or equal to MinComplexity"));
    }

    [Fact]
    public void Validate_MaxComplexityEqualToMinComplexity_Passes()
    {
        var query = BuildValidQuery(minComplexity: 3m, maxComplexity: 3m);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullComplexityBounds_Pass()
    {
        var query = BuildValidQuery(minComplexity: null, maxComplexity: null);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // Players
    // -------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Validate_MinPlayersPositive_Passes(int minPlayers)
    {
        var result = _validator.Validate(BuildValidQuery(minPlayers: minPlayers));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MinPlayersNonPositive_Fails(int minPlayers)
    {
        var result = _validator.Validate(BuildValidQuery(minPlayers: minPlayers));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SearchSharedGamesQuery.MinPlayers));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void Validate_MaxPlayersPositive_Passes(int maxPlayers)
    {
        var result = _validator.Validate(BuildValidQuery(maxPlayers: maxPlayers));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_MaxPlayersNonPositive_Fails(int maxPlayers)
    {
        var result = _validator.Validate(BuildValidQuery(maxPlayers: maxPlayers));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SearchSharedGamesQuery.MaxPlayers));
    }

    [Fact]
    public void Validate_MaxPlayersLowerThanMinPlayers_Fails()
    {
        var query = BuildValidQuery(minPlayers: 4, maxPlayers: 2);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("MaxPlayers must be greater than or equal to MinPlayers"));
    }

    [Fact]
    public void Validate_MaxPlayersEqualToMinPlayers_Passes()
    {
        var query = BuildValidQuery(minPlayers: 4, maxPlayers: 4);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullPlayerBounds_Pass()
    {
        var query = BuildValidQuery(minPlayers: null, maxPlayers: null);

        var result = _validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    // -------------------------------------------------------------------
    // Playing time
    // -------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(480)]
    public void Validate_MaxPlayingTimePositive_Passes(int maxPlayingTime)
    {
        var result = _validator.Validate(BuildValidQuery(maxPlayingTime: maxPlayingTime));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxPlayingTimeNonPositive_Fails(int maxPlayingTime)
    {
        var result = _validator.Validate(BuildValidQuery(maxPlayingTime: maxPlayingTime));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(SearchSharedGamesQuery.MaxPlayingTime));
    }

    [Fact]
    public void Validate_NullMaxPlayingTime_Passes()
    {
        var result = _validator.Validate(BuildValidQuery(maxPlayingTime: null));

        result.IsValid.Should().BeTrue();
    }
}
