using Api.BoundedContexts.GameManagement.Application.Handlers.GameNight;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.Infrastructure.ExternalServices.BoardGameGeek.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// Unit tests for SearchBggGamesForGameNightQueryHandler.
/// Validates BGG search, pagination, and result mapping for the Game Night Improvvisata flow.
/// Game Night Improvvisata - E1-1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class SearchBggGamesForGameNightQueryHandlerTests
{
    private readonly Mock<IBggApiClient> _mockBggApiClient;
    private readonly Mock<ILogger<SearchBggGamesForGameNightQueryHandler>> _mockLogger;
    private readonly SearchBggGamesForGameNightQueryHandler _handler;

    public SearchBggGamesForGameNightQueryHandlerTests()
    {
        _mockBggApiClient = new Mock<IBggApiClient>();
        _mockLogger = new Mock<ILogger<SearchBggGamesForGameNightQueryHandler>>();
        _handler = new SearchBggGamesForGameNightQueryHandler(
            _mockBggApiClient.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidSearchTerm_ReturnsMappedResults()
    {
        // Arrange
        var bggResults = new List<BggSearchResult>
        {
            new() { BggId = 174430, Name = "Gloomhaven", YearPublished = 2017, ThumbnailUrl = "https://example.com/gloom.jpg" },
            new() { BggId = 224517, Name = "Brass: Birmingham", YearPublished = 2018, ThumbnailUrl = null }
        };

        _mockBggApiClient
            .Setup(c => c.SearchGamesAsync("Gloomhaven", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggResults);

        var query = new SearchBggGamesForGameNightQuery("Gloomhaven");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Results.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithValidSearchTerm_MapsBggFieldsCorrectly()
    {
        // Arrange
        var bggResults = new List<BggSearchResult>
        {
            new() { BggId = 174430, Name = "Gloomhaven", YearPublished = 2017, ThumbnailUrl = "https://example.com/gloom.jpg", Type = "boardgame" }
        };

        _mockBggApiClient
            .Setup(c => c.SearchGamesAsync("Gloomhaven", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggResults);

        var query = new SearchBggGamesForGameNightQuery("Gloomhaven");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var first = result.Results[0];
        first.BggId.Should().Be(174430);
        first.Title.Should().Be("Gloomhaven");
        first.YearPublished.Should().Be(2017);
        first.ThumbnailUrl.Should().Be("https://example.com/gloom.jpg");
    }

    [Fact]
    public async Task Handle_WithEmptyBggResults_ReturnsEmptyPagedResult()
    {
        // Arrange
        _mockBggApiClient
            .Setup(c => c.SearchGamesAsync("NonExistentGame", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BggSearchResult>());

        var query = new SearchBggGamesForGameNightQuery("NonExistentGame");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.Results.Should().BeEmpty();
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var bggResults = Enumerable.Range(1, 25)
            .Select(i => new BggSearchResult { BggId = i, Name = $"Game {i}", YearPublished = 2000 + i })
            .ToList();

        _mockBggApiClient
            .Setup(c => c.SearchGamesAsync("Game", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggResults);

        var query = new SearchBggGamesForGameNightQuery("Game", Page: 2, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Total.Should().Be(25);
        result.Results.Should().HaveCount(10);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(3);
        result.Results[0].BggId.Should().Be(11); // Second page starts at item 11
    }

    [Fact]
    public async Task Handle_WithLastPagePartialResults_ReturnsRemainingItems()
    {
        // Arrange
        var bggResults = Enumerable.Range(1, 25)
            .Select(i => new BggSearchResult { BggId = i, Name = $"Game {i}" })
            .ToList();

        _mockBggApiClient
            .Setup(c => c.SearchGamesAsync("Game", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bggResults);

        var query = new SearchBggGamesForGameNightQuery("Game", Page: 3, PageSize: 10);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Total.Should().Be(25);
        result.Results.Should().HaveCount(5); // Only 5 items remain on page 3
        result.TotalPages.Should().Be(3);
    }
}
