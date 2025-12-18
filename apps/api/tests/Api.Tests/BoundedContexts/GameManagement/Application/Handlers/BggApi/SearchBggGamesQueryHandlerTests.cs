using Api.BoundedContexts.GameManagement.Application.Queries.BggApi;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.BggApi;

/// <summary>
/// Tests for SearchBggGamesQueryHandler.
/// Tests search validation, empty query handling, and BGG API delegation.
/// Issue #2190: Add unit tests for BoardGameGeek integration handlers.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SearchBggGamesQueryHandlerTests
{
    private readonly Mock<IBggApiService> _mockBggApiService;
    private readonly Mock<ILogger<SearchBggGamesQueryHandler>> _mockLogger;
    private readonly SearchBggGamesQueryHandler _handler;

    public SearchBggGamesQueryHandlerTests()
    {
        _mockBggApiService = new Mock<IBggApiService>();
        _mockLogger = new Mock<ILogger<SearchBggGamesQueryHandler>>();
        _handler = new SearchBggGamesQueryHandler(
            _mockBggApiService.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange & Act
        var handler = new SearchBggGamesQueryHandler(
            _mockBggApiService.Object,
            _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullBggApiService_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new SearchBggGamesQueryHandler(
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("bggApiService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new SearchBggGamesQueryHandler(
            _mockBggApiService.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Handle Tests - Empty/Whitespace Queries

    [Fact]
    public async Task Handle_WithEmptyQuery_ReturnsEmptyList()
    {
        // Arrange
        var query = new SearchBggGamesQuery { Query = string.Empty, Exact = false };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
        _mockBggApiService.Verify(
            s => s.SearchGamesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceQuery_ReturnsEmptyList()
    {
        // Arrange
        var query = new SearchBggGamesQuery { Query = "   ", Exact = false };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
        _mockBggApiService.Verify(
            s => s.SearchGamesAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ReturnsEmptyList()
    {
        // Arrange
        var query = new SearchBggGamesQuery { Query = null!, Exact = false };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region Handle Tests - Valid Queries

    [Fact]
    public async Task Handle_WithValidQuery_ReturnsBggSearchResults()
    {
        // Arrange
        var expectedResults = new List<BggSearchResultDto>
        {
            new(174430, "Gloomhaven", 2017, "https://example.com/thumb.jpg", "boardgame"),
            new(224517, "Brass: Birmingham", 2018, null, "boardgame")
        };

        _mockBggApiService
            .Setup(s => s.SearchGamesAsync("Gloomhaven", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var query = new SearchBggGamesQuery { Query = "Gloomhaven", Exact = false };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].BggId.Should().Be(174430);
        result[0].Name.Should().Be("Gloomhaven");
        result[0].YearPublished.Should().Be(2017);
    }

    [Fact]
    public async Task Handle_WithExactMatchFlag_PassesExactToService()
    {
        // Arrange
        var expectedResults = new List<BggSearchResultDto>
        {
            new(13, "Catan", 1995, null, "boardgame")
        };

        _mockBggApiService
            .Setup(s => s.SearchGamesAsync("Catan", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        var query = new SearchBggGamesQuery { Query = "Catan", Exact = true };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        _mockBggApiService.Verify(
            s => s.SearchGamesAsync("Catan", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsEmptyList_ReturnsEmptyList()
    {
        // Arrange
        _mockBggApiService
            .Setup(s => s.SearchGamesAsync("NonExistentGame", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<BggSearchResultDto>());

        var query = new SearchBggGamesQuery { Query = "NonExistentGame", Exact = false };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region Handle Tests - Cancellation

    [Fact]
    public async Task Handle_WithCancellationToken_PassesCancellationToService()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _mockBggApiService
            .Setup(s => s.SearchGamesAsync("Test", false, token))
            .ReturnsAsync(new List<BggSearchResultDto>());

        var query = new SearchBggGamesQuery { Query = "Test", Exact = false };

        // Act
        await _handler.Handle(query, token);

        // Assert
        _mockBggApiService.Verify(
            s => s.SearchGamesAsync("Test", false, token),
            Times.Once);
    }

    #endregion

    #region Query Record Tests

    [Fact]
    public void SearchBggGamesQuery_WithDefaultValues_HasExpectedDefaults()
    {
        // Arrange & Act
        var query = new SearchBggGamesQuery();

        // Assert
        query.Query.Should().BeEmpty();
        query.Exact.Should().BeFalse();
    }

    [Fact]
    public void SearchBggGamesQuery_WithValues_StoresCorrectly()
    {
        // Arrange & Act
        var query = new SearchBggGamesQuery { Query = "Pandemic", Exact = true };

        // Assert
        query.Query.Should().Be("Pandemic");
        query.Exact.Should().BeTrue();
    }

    #endregion
}
