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
/// Tests for GetBggGameDetailsQueryHandler.
/// Retrieves detailed board game information from BoardGameGeek API.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetBggGameDetailsQueryHandlerTests
{
    private readonly Mock<IBggApiService> _bggApiServiceMock;
    private readonly Mock<ILogger<GetBggGameDetailsQueryHandler>> _loggerMock;
    private readonly GetBggGameDetailsQueryHandler _handler;

    public GetBggGameDetailsQueryHandlerTests()
    {
        _bggApiServiceMock = new Mock<IBggApiService>();
        _loggerMock = new Mock<ILogger<GetBggGameDetailsQueryHandler>>();
        _handler = new GetBggGameDetailsQueryHandler(
            _bggApiServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidBggId_ReturnsGameDetails()
    {
        // Arrange
        var bggId = 12345;
        var query = new GetBggGameDetailsQuery(bggId);
        var expectedDetails = CreateBggGameDetails(bggId);

        _bggApiServiceMock
            .Setup(s => s.GetGameDetailsAsync(bggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(bggId, result.BggId);
        Assert.Equal("Test Game", result.Name);
    }

    [Fact]
    public async Task Handle_WithNonExistentBggId_ReturnsNull()
    {
        // Arrange
        var bggId = 99999;
        var query = new GetBggGameDetailsQuery(bggId);

        _bggApiServiceMock
            .Setup(s => s.GetGameDetailsAsync(bggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BggGameDetailsDto?)null);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_WithInvalidBggId_ThrowsArgumentException()
    {
        // Arrange
        var invalidBggId = 0;
        var query = new GetBggGameDetailsQuery(invalidBggId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("BGG ID must be positive", exception.Message);
    }

    [Fact]
    public async Task Handle_WithNegativeBggId_ThrowsArgumentException()
    {
        // Arrange
        var negativeBggId = -1;
        var query = new GetBggGameDetailsQuery(negativeBggId);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(query, TestContext.Current.CancellationToken));

        Assert.Contains("BGG ID must be positive", exception.Message);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToService()
    {
        // Arrange
        var bggId = 12345;
        var query = new GetBggGameDetailsQuery(bggId);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _bggApiServiceMock
            .Setup(s => s.GetGameDetailsAsync(bggId, token))
            .ReturnsAsync(CreateBggGameDetails(bggId));

        // Act
        await _handler.Handle(query, token);

        // Assert
        _bggApiServiceMock.Verify(s => s.GetGameDetailsAsync(bggId, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    private static BggGameDetailsDto CreateBggGameDetails(int bggId)
    {
        return new BggGameDetailsDto(
            BggId: bggId,
            Name: "Test Game",
            Description: "A test board game",
            YearPublished: 2020,
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTime: 60,
            MinPlayTime: 45,
            MaxPlayTime: 90,
            MinAge: 10,
            AverageRating: 7.5,
            BayesAverageRating: 7.3,
            UsersRated: 1000,
            AverageWeight: 2.5,
            ThumbnailUrl: "https://example.com/thumb.png",
            ImageUrl: "https://example.com/image.png",
            Categories: new List<string> { "Strategy" },
            Mechanics: new List<string> { "Worker Placement" },
            Designers: new List<string> { "Test Designer" },
            Publishers: new List<string> { "Test Publisher" }
        );
    }
}
