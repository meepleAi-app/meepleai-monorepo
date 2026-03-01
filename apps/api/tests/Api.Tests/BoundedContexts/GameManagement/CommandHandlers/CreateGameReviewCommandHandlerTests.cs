using Api.BoundedContexts.GameManagement.Application.Commands.GameReviews;
using Api.BoundedContexts.GameManagement.Application.Handlers.GameReviews;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.CommandHandlers;

/// <summary>
/// Unit tests for CreateGameReviewCommandHandler.
/// Issue #4904: Game reviews API endpoint.
/// </summary>
public sealed class CreateGameReviewCommandHandlerTests
{
    private readonly Mock<IGameReviewRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly FakeTimeProvider _timeProvider;
    private readonly CreateGameReviewCommandHandler _sut;

    public CreateGameReviewCommandHandlerTests()
    {
        _repositoryMock = new Mock<IGameReviewRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _timeProvider = new FakeTimeProvider(
            new DateTimeOffset(2025, 6, 15, 10, 0, 0, TimeSpan.Zero));
        _sut = new CreateGameReviewCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider);
    }

    [Fact]
    public async Task Handle_CreatesReview_WhenUserHasNoExistingReview()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.FindByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameReview?)null);

        var command = new CreateGameReviewCommand(gameId, userId, "Alice", 8, "Great game!");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.GameId);
        Assert.Equal("Alice", result.AuthorName);
        Assert.Equal(8, result.Rating);
        Assert.Equal("Great game!", result.Content);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc), result.CreatedAt);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<GameReview>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsConflictException_WhenUserAlreadyReviewed()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var existingReview = GameReview.Reconstitute(
            Guid.NewGuid(), gameId, userId, "Alice", 8, "Old review",
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        _repositoryMock
            .Setup(r => r.FindByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingReview);

        var command = new CreateGameReviewCommand(gameId, userId, "Alice", 9, "Updated thoughts");

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.Handle(command, CancellationToken.None));

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<GameReview>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _sut.Handle(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task Handle_AcceptsValidRatings(int rating)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.FindByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameReview?)null);

        var command = new CreateGameReviewCommand(gameId, userId, "Tester", rating, "Content");

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(rating, result.Rating);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken_ToRepository()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _repositoryMock
            .Setup(r => r.FindByUserAndGameAsync(userId, gameId, token))
            .ReturnsAsync((GameReview?)null);

        var command = new CreateGameReviewCommand(gameId, userId, "Alice", 7, "Good game");

        // Act
        await _sut.Handle(command, token);

        // Assert
        _repositoryMock.Verify(r => r.FindByUserAndGameAsync(userId, gameId, token), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(token), Times.Once);
    }
}
