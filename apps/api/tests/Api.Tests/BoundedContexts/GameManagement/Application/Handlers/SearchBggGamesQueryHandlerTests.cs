using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for SearchBggGamesQueryHandler.
/// Searches BoardGameGeek API for board games by name.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SearchBggGamesQueryHandlerTests
{
    private readonly Mock<IBggApiService> _bggApiServiceMock;
    private readonly Mock<ILogger<SearchBggGamesQueryHandler>> _loggerMock;
    private readonly SearchBggGamesQueryHandler _handler;

    public SearchBggGamesQueryHandlerTests()
    {
        _bggApiServiceMock = new Mock<IBggApiService>();
        _loggerMock = new Mock<ILogger<SearchBggGamesQueryHandler>>();
        _handler = new SearchBggGamesQueryHandler(
            _bggApiServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidSearchTerm_ReturnsResults()
    {
        // Arrange
        var searchTerm = "Catan";
        var query = new SearchBggGamesQuery(searchTerm, false);
        var expectedResults = new List<BggSearchResultDto>
        {
            CreateBggSearchResult(1, "Catan"),
            CreateBggSearchResult(2, "Catan: Seafarers")
        };

        _bggApiServiceMock
            .Setup(s => s.SearchGamesAsync(searchTerm, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Name == "Catan");
    }

    [Fact]
    public async Task Handle_WithExactMatch_CallsServiceWithExactMatch()
    {
        // Arrange
        var searchTerm = "Catan";
        var query = new SearchBggGamesQuery(searchTerm, true);

        _bggApiServiceMock
            .Setup(s => s.SearchGamesAsync(searchTerm, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BggSearchResultDto>());

        // Act
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        _bggApiServiceMock.Verify(
            s => s.SearchGamesAsync(searchTerm, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptySearchTerm_ReturnsEmptyList()
    {
        // Arrange
        var query = new SearchBggGamesQuery(string.Empty, false);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
        _bggApiServiceMock.Verify(
            s => s.SearchGamesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceSearchTerm_ReturnsEmptyList()
    {
        // Arrange
        var query = new SearchBggGamesQuery("   ", false);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithNoResults_ReturnsEmptyList()
    {
        // Arrange
        var searchTerm = "NonExistentGame12345";
        var query = new SearchBggGamesQuery(searchTerm, false);

        _bggApiServiceMock
            .Setup(s => s.SearchGamesAsync(searchTerm, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BggSearchResultDto>());

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToService()
    {
        // Arrange
        var searchTerm = "Test";
        var query = new SearchBggGamesQuery(searchTerm, false);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _bggApiServiceMock
            .Setup(s => s.SearchGamesAsync(searchTerm, false, token))
            .ReturnsAsync(new List<BggSearchResultDto>());

        // Act
        await _handler.Handle(query, token);

        // Assert
        _bggApiServiceMock.Verify(s => s.SearchGamesAsync(searchTerm, false, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    private static BggSearchResultDto CreateBggSearchResult(int bggId, string name)
    {
        return new BggSearchResultDto(
            BggId: bggId,
            Name: name,
            YearPublished: 2020,
            ThumbnailUrl: $"https://example.com/thumb_{bggId}.png",
            Type: "boardgame"
        );
    }
}
