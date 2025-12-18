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
/// Tests for GetBggGameDetailsQueryHandler.
/// Tests BggId validation, null handling, and BGG API delegation.
/// Issue #2190: Add unit tests for BoardGameGeek integration handlers.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetBggGameDetailsQueryHandlerTests
{
    private readonly Mock<IBggApiService> _mockBggApiService;
    private readonly Mock<ILogger<GetBggGameDetailsQueryHandler>> _mockLogger;
    private readonly GetBggGameDetailsQueryHandler _handler;

    public GetBggGameDetailsQueryHandlerTests()
    {
        _mockBggApiService = new Mock<IBggApiService>();
        _mockLogger = new Mock<ILogger<GetBggGameDetailsQueryHandler>>();
        _handler = new GetBggGameDetailsQueryHandler(
            _mockBggApiService.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange & Act
        var handler = new GetBggGameDetailsQueryHandler(
            _mockBggApiService.Object,
            _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullBggApiService_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new GetBggGameDetailsQueryHandler(
            null!,
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("bggApiService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new GetBggGameDetailsQueryHandler(
            _mockBggApiService.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Handle Tests - Invalid BggId

    [Fact]
    public async Task Handle_WithZeroBggId_ReturnsNull()
    {
        // Arrange
        var query = new GetBggGameDetailsQuery { BggId = 0 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _mockBggApiService.Verify(
            s => s.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNegativeBggId_ReturnsNull()
    {
        // Arrange
        var query = new GetBggGameDetailsQuery { BggId = -1 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _mockBggApiService.Verify(
            s => s.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-999999)]
    [InlineData(int.MinValue)]
    public async Task Handle_WithVariousNegativeBggIds_ReturnsNull(int bggId)
    {
        // Arrange
        var query = new GetBggGameDetailsQuery { BggId = bggId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => _handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Handle Tests - Valid BggId

    [Fact]
    public async Task Handle_WithValidBggId_ReturnsBggGameDetails()
    {
        // Arrange
        var expectedDetails = CreateSampleGameDetails(174430, "Gloomhaven");

        _mockBggApiService
            .Setup(s => s.GetGameDetailsAsync(174430, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        var query = new GetBggGameDetailsQuery { BggId = 174430 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.BggId.Should().Be(174430);
        result.Name.Should().Be("Gloomhaven");
        result.YearPublished.Should().Be(2017);
        result.MinPlayers.Should().Be(1);
        result.MaxPlayers.Should().Be(4);
    }

    [Fact]
    public async Task Handle_WithValidBggId_ReturnsCompleteGameDetails()
    {
        // Arrange
        var expectedDetails = new BggGameDetailsDto(
            BggId: 224517,
            Name: "Brass: Birmingham",
            Description: "Birmingham — the Industrial Revolution",
            YearPublished: 2018,
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTime: 120,
            MinPlayTime: 60,
            MaxPlayTime: 120,
            MinAge: 14,
            AverageRating: 8.65,
            BayesAverageRating: 8.42,
            UsersRated: 55000,
            AverageWeight: 3.91,
            ThumbnailUrl: "https://cf.geekdo-images.com/thumb.jpg",
            ImageUrl: "https://cf.geekdo-images.com/full.jpg",
            Categories: new List<string> { "Economic", "Industry / Manufacturing" },
            Mechanics: new List<string> { "Hand Management", "Network Building" },
            Designers: new List<string> { "Gavan Brown", "Matt Tolman", "Martin Wallace" },
            Publishers: new List<string> { "Roxley" });

        _mockBggApiService
            .Setup(s => s.GetGameDetailsAsync(224517, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDetails);

        var query = new GetBggGameDetailsQuery { BggId = 224517 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.BggId.Should().Be(224517);
        result.Name.Should().Be("Brass: Birmingham");
        result.Description.Should().Contain("Birmingham");
        result.YearPublished.Should().Be(2018);
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTime.Should().Be(120);
        result.MinAge.Should().Be(14);
        result.AverageRating.Should().BeApproximately(8.65, 0.01);
        result.AverageWeight.Should().BeApproximately(3.91, 0.01);
        result.Categories.Should().Contain("Economic");
        result.Mechanics.Should().Contain("Hand Management");
        result.Designers.Should().Contain("Martin Wallace");
        result.Publishers.Should().Contain("Roxley");
    }

    [Fact]
    public async Task Handle_WhenServiceReturnsNull_ReturnsNull()
    {
        // Arrange
        _mockBggApiService
            .Setup(s => s.GetGameDetailsAsync(999999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BggGameDetailsDto?)null);

        var query = new GetBggGameDetailsQuery { BggId = 999999 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public async Task Handle_WithVariousValidBggIds_CallsService(int bggId)
    {
        // Arrange
        _mockBggApiService
            .Setup(s => s.GetGameDetailsAsync(bggId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BggGameDetailsDto?)null);

        var query = new GetBggGameDetailsQuery { BggId = bggId };

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockBggApiService.Verify(
            s => s.GetGameDetailsAsync(bggId, It.IsAny<CancellationToken>()),
            Times.Once);
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
            .Setup(s => s.GetGameDetailsAsync(174430, token))
            .ReturnsAsync((BggGameDetailsDto?)null);

        var query = new GetBggGameDetailsQuery { BggId = 174430 };

        // Act
        await _handler.Handle(query, token);

        // Assert
        _mockBggApiService.Verify(
            s => s.GetGameDetailsAsync(174430, token),
            Times.Once);
    }

    #endregion

    #region Query Record Tests

    [Fact]
    public void GetBggGameDetailsQuery_WithDefaultValues_HasZeroBggId()
    {
        // Arrange & Act
        var query = new GetBggGameDetailsQuery();

        // Assert
        query.BggId.Should().Be(0);
    }

    [Fact]
    public void GetBggGameDetailsQuery_WithValue_StoresCorrectly()
    {
        // Arrange & Act
        var query = new GetBggGameDetailsQuery { BggId = 174430 };

        // Assert
        query.BggId.Should().Be(174430);
    }

    [Fact]
    public void GetBggGameDetailsQuery_Equality_WorksCorrectly()
    {
        // Arrange
        var query1 = new GetBggGameDetailsQuery { BggId = 174430 };
        var query2 = new GetBggGameDetailsQuery { BggId = 174430 };
        var query3 = new GetBggGameDetailsQuery { BggId = 224517 };

        // Assert
        query1.Should().Be(query2);
        query1.Should().NotBe(query3);
    }

    #endregion

    #region Helper Methods

    private static BggGameDetailsDto CreateSampleGameDetails(int bggId, string name)
    {
        return new BggGameDetailsDto(
            BggId: bggId,
            Name: name,
            Description: $"Description for {name}",
            YearPublished: 2017,
            MinPlayers: 1,
            MaxPlayers: 4,
            PlayingTime: 120,
            MinPlayTime: 60,
            MaxPlayTime: 120,
            MinAge: 14,
            AverageRating: 8.5,
            BayesAverageRating: 8.3,
            UsersRated: 50000,
            AverageWeight: 3.87,
            ThumbnailUrl: $"https://example.com/{bggId}/thumb.jpg",
            ImageUrl: $"https://example.com/{bggId}/full.jpg",
            Categories: new List<string> { "Adventure", "Fantasy" },
            Mechanics: new List<string> { "Campaign", "Hand Management" },
            Designers: new List<string> { "Isaac Childres" },
            Publishers: new List<string> { "Cephalofair Games" });
    }

    #endregion
}
