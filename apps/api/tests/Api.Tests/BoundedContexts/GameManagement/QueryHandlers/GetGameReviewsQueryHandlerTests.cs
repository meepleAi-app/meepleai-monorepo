using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries.GameReviews;
using Api.BoundedContexts.GameManagement.Application.Queries.GameReviews;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.QueryHandlers;

/// <summary>
/// Unit tests for GetGameReviewsQueryHandler.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
public sealed class GetGameReviewsQueryHandlerTests
{
    private readonly Mock<IGameReviewRepository> _repositoryMock;
    private readonly GetGameReviewsQueryHandler _sut;

    public GetGameReviewsQueryHandlerTests()
    {
        _repositoryMock = new Mock<IGameReviewRepository>();
        _sut = new GetGameReviewsQueryHandler(
            _repositoryMock.Object,
            NullLogger<GetGameReviewsQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsPagedResult_WhenReviewsExist()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var reviews = new List<GameReview>
        {
            CreateReview(gameId, Guid.NewGuid(), "Alice", 9, "Amazing game!"),
            CreateReview(gameId, Guid.NewGuid(), "Bob", 7, "Pretty fun but complex.")
        };

        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(gameId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((reviews, 2));

        var query = new GetGameReviewsQuery(gameId, PageNumber: 1, PageSize: 10);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);

        var first = result.Items[0];
        Assert.Equal("Alice", first.AuthorName);
        Assert.Equal(9, first.Rating);
        Assert.Equal("Amazing game!", first.Content);
        Assert.Equal(gameId, first.GameId);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyResult_WhenNoReviewsExist()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetBySharedGameIdAsync(gameId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<GameReview>(), 0));

        var query = new GetGameReviewsQuery(gameId);

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
            .ReturnsAsync((new List<GameReview>(), 0));

        var query = new GetGameReviewsQuery(gameId, inputPage, inputSize);

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
            .ReturnsAsync((new List<GameReview>(), 0));

        var query = new GetGameReviewsQuery(gameId);

        // Act
        await _sut.Handle(query, token);

        // Assert
        _repositoryMock.Verify(r => r.GetBySharedGameIdAsync(gameId, 1, 10, token), Times.Once);
    }

    private static GameReview CreateReview(
        Guid gameId,
        Guid userId,
        string authorName,
        int rating,
        string content)
    {
        return GameReview.Reconstitute(
            Guid.NewGuid(),
            gameId,
            userId,
            authorName,
            rating,
            content,
            new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
    }
}
