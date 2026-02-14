// SearchGamesQueryUnitTests - Unit tests for SearchGamesQuery record
//
// Tests the query record structure and validation logic.
// Integration tests deferred due to EF Core translation issue with StringComparison.OrdinalIgnoreCase.
//
// Issue #4273: Game Search Autocomplete
// Known Issue: Query uses Contains(query, StringComparison.OrdinalIgnoreCase) which doesn't translate to SQL.
//              Should use EF.Functions.ILike() for PostgreSQL case-insensitive search.

using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Queries;

/// <summary>
/// Unit tests for SearchGamesQuery record.
/// Tests query structure and default values.
/// Issue #4273: Game Search Autocomplete
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4273")]
public class SearchGamesQueryUnitTests
{
    #region Record Creation Tests

    [Fact]
    public void SearchGamesQuery_CreatesWithRequiredProperties()
    {
        // Arrange
        var query = "Catan";
        var userId = Guid.NewGuid();

        // Act
        var searchQuery = new SearchGamesQuery(query, userId);

        // Assert
        searchQuery.Query.Should().Be(query);
        searchQuery.UserId.Should().Be(userId);
    }

    [Fact]
    public void SearchGamesQuery_DefaultMaxResultsIs20()
    {
        // Arrange & Act
        var searchQuery = new SearchGamesQuery("Test", Guid.NewGuid());

        // Assert
        searchQuery.MaxResults.Should().Be(20);
    }

    [Fact]
    public void SearchGamesQuery_CanSetCustomMaxResults()
    {
        // Arrange & Act
        var searchQuery = new SearchGamesQuery("Test", Guid.NewGuid(), MaxResults: 50);

        // Assert
        searchQuery.MaxResults.Should().Be(50);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void SearchGamesQuery_WithSameValues_AreEqual()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query1 = new SearchGamesQuery("Catan", userId, 20);
        var query2 = new SearchGamesQuery("Catan", userId, 20);

        // Act & Assert
        query1.Should().Be(query2);
    }

    [Fact]
    public void SearchGamesQuery_WithDifferentQuery_AreNotEqual()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query1 = new SearchGamesQuery("Catan", userId);
        var query2 = new SearchGamesQuery("Carcassonne", userId);

        // Act & Assert
        query1.Should().NotBe(query2);
    }

    [Fact]
    public void SearchGamesQuery_WithDifferentUserId_AreNotEqual()
    {
        // Arrange
        var query1 = new SearchGamesQuery("Catan", Guid.NewGuid());
        var query2 = new SearchGamesQuery("Catan", Guid.NewGuid());

        // Act & Assert
        query1.Should().NotBe(query2);
    }

    [Fact]
    public void SearchGamesQuery_WithDifferentMaxResults_AreNotEqual()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query1 = new SearchGamesQuery("Catan", userId, MaxResults: 10);
        var query2 = new SearchGamesQuery("Catan", userId, MaxResults: 20);

        // Act & Assert
        query1.Should().NotBe(query2);
    }

    #endregion

    #region Edge Case Tests

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void SearchGamesQuery_CanBeCreatedWithEmptyOrWhitespaceQuery(string query)
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var searchQuery = new SearchGamesQuery(query, userId);

        // Assert
        searchQuery.Query.Should().Be(query);
    }

    [Fact]
    public void SearchGamesQuery_CanBeCreatedWithNullQuery()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var searchQuery = new SearchGamesQuery(null!, userId);

        // Assert
        searchQuery.Query.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void SearchGamesQuery_SupportsVariousMaxResultsValues(int maxResults)
    {
        // Arrange & Act
        var searchQuery = new SearchGamesQuery("Test", Guid.NewGuid(), MaxResults: maxResults);

        // Assert
        searchQuery.MaxResults.Should().Be(maxResults);
    }

    #endregion

    #region Special Characters Tests

    [Theory]
    [InlineData("Dungeons & Dragons")]
    [InlineData("7 Wonders")]
    [InlineData("Catan: Cities & Knights")]
    [InlineData("Bang!")]
    [InlineData("Ticket to Ride: Europe")]
    public void SearchGamesQuery_HandlesSpecialCharactersInQuery(string query)
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var searchQuery = new SearchGamesQuery(query, userId);

        // Assert
        searchQuery.Query.Should().Be(query);
    }

    #endregion
}
