using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.GameStrategies;
using Api.BoundedContexts.GameManagement.Application.Queries.GameStrategies;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

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
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Items.Count.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);

        var first = result.Items[0];
        first.Title.Should().Be("Opening Moves");
        first.Content.Should().Be("Control the center early.");
        first.Author.Should().Be("Alice");
        first.Upvotes.Should().Be(42);
        first.GameId.Should().Be(gameId);
        first.Tags.Should().Contain("opening");
        first.Tags.Should().Contain("tactics");
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
        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ThrowsArgumentNullException_WhenQueryIsNull()
    {
        // Act & Assert
        var act = () =>
            _sut.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Page.Should().Be(expectedPage);
        result.PageSize.Should().Be(expectedSize);
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
