using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Tests for GetLibraryStatsQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetLibraryStatsQueryHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _repositoryMock = new();
    private readonly GetLibraryStatsQueryHandler _handler;

    public GetLibraryStatsQueryHandlerTests()
    {
        _handler = new GetLibraryStatsQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        Func<Task> act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_ValidQuery_AggregatesAllRepositoryCountsIntoDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldest = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newest = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);

        _repositoryMock
            .Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _repositoryMock
            .Setup(r => r.GetFavoriteCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _repositoryMock
            .Setup(r => r.GetPrivatePdfCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        _repositoryMock
            .Setup(r => r.GetLibraryDateRangeAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((oldest, newest));
        _repositoryMock
            .Setup(r => r.GetStateCountsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<GameStateType, int>
            {
                [GameStateType.Owned] = 5,
                [GameStateType.Wishlist] = 4,
                [GameStateType.InPrestito] = 1,
                [GameStateType.Nuovo] = 0,
            });

        // Act
        var result = await _handler.Handle(new GetLibraryStatsQuery(userId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalGames.Should().Be(10);
        result.FavoriteGames.Should().Be(3);
        result.PrivatePdfs.Should().Be(2);
        result.OldestAddedAt.Should().Be(oldest);
        result.NewestAddedAt.Should().Be(newest);
        result.OwnedCount.Should().Be(5);
        result.WishlistCount.Should().Be(4);
        result.InPrestitoCount.Should().Be(1);
        result.NuovoCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_EmptyLibrary_ReturnsZeroCountsAndNullDates()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _repositoryMock
            .Setup(r => r.GetUserLibraryCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repositoryMock
            .Setup(r => r.GetFavoriteCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repositoryMock
            .Setup(r => r.GetPrivatePdfCountAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repositoryMock
            .Setup(r => r.GetLibraryDateRangeAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((DateTime?)null, (DateTime?)null));
        _repositoryMock
            .Setup(r => r.GetStateCountsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<GameStateType, int>());

        // Act
        var result = await _handler.Handle(new GetLibraryStatsQuery(userId), TestContext.Current.CancellationToken);

        // Assert
        result.TotalGames.Should().Be(0);
        result.FavoriteGames.Should().Be(0);
        result.PrivatePdfs.Should().Be(0);
        result.OldestAddedAt.Should().BeNull();
        result.NewestAddedAt.Should().BeNull();
        result.OwnedCount.Should().Be(0);
        result.WishlistCount.Should().Be(0);
        result.InPrestitoCount.Should().Be(0);
        result.NuovoCount.Should().Be(0);
    }
}
