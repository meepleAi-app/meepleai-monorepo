using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.GameStrategies;
using Api.BoundedContexts.GameManagement.Application.Queries.GameStrategies;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.QueryHandlers;

/// <summary>
/// Unit tests for GetGameStrategiesQueryHandler.
/// Issue #4903: Game strategies API endpoint.
/// </summary>
public sealed class GetGameStrategiesQueryHandlerTests
{
    private readonly Mock<IGameStrategyRepository> _repositoryMock;
    private readonly GetGameStrategiesQueryHandler _sut;

    public GetGameStrategiesQueryHandlerTests()
    {
        _repositoryMock = new Mock<IGameStrategyRepository>();
        _sut = new GetGameStrategiesQueryHandler(
            _repositoryMock.Object,
            NullLogger<GetGameStrategiesQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsPagedResult_WhenStrategiesExist()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var strategies = new List<GameStrategy>
        {
            CreateStrategy(gameId, "Opening Moves", "Control the center early.", "Alice", 42, ["opening", "tactics"]),
            CreateStrategy(gameId, "Defense Tips", "Protect your king side.", "Bob", 18, ["defense"])
        };

        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(gameId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((strategies, 2));

        var query = new GetGameStrategiesQuery(gameId, PageNumber: 1, PageSize: 10);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);

        var first = result.Items[0];
        Assert.Equal("Opening Moves", first.Title);
        Assert.Equal("Control the center early.", first.Content);
        Assert.Equal("Alice", first.Author);
        Assert.Equal(42, first.Upvotes);
        Assert.Equal(gameId, first.GameId);
        Assert.Contains("opening", first.Tags);
        Assert.Contains("tactics", first.Tags);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyResult_WhenNoStrategiesExist()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(gameId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<GameStrategy>(), 0));

        var query = new GetGameStrategiesQuery(gameId);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(0, 10, 1, 10)]   // Page 0 → clamped to 1
    [InlineData(-1, 5, 1, 5)]    // Negative page → clamped to 1
    [InlineData(2, 200, 2, 100)] // PageSize > 100 → clamped to 100
    [InlineData(3, 0, 3, 1)]     // PageSize 0 → clamped to 1
    public async Task Handle_ClampsPageNumberAndSize(
        int inputPage, int inputSize, int expectedPage, int expectedSize)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(gameId, expectedPage, expectedSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<GameStrategy>(), 0));

        var query = new GetGameStrategiesQuery(gameId, inputPage, inputSize);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(expectedPage, result.Page);
        Assert.Equal(expectedSize, result.PageSize);
        _repositoryMock.Verify(
            r => r.GetBySharedGameIdAsync(gameId, expectedPage, expectedSize, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(gameId, 1, 10, token))
            .ReturnsAsync((new List<GameStrategy>(), 0));

        var query = new GetGameStrategiesQuery(gameId);

        // Act
        await _sut.Handle(query, token);

        // Assert
        _repositoryMock.Verify(r => r.GetBySharedGameIdAsync(gameId, 1, 10, token), Times.Once);
    }

    // Helper: Reconstitute a GameStrategy via its internal factory
    private static GameStrategy CreateStrategy(
        Guid gameId,
        string title,
        string content,
        string author,
        int upvotes,
        IReadOnlyList<string> tags)
    {
        return GameStrategy.Reconstitute(
            Guid.NewGuid(),
            gameId,
            title,
            content,
            author,
            upvotes,
            tags,
            new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
    }
}
