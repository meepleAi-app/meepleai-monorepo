using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for GetLibraryForDowngradeQueryHandler.
/// Verifies priority sorting and keep/remove splitting logic for tier downgrade flow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class GetLibraryForDowngradeQueryHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _repoMock;
    private readonly MeepleAiDbContext _db;
    private readonly GetLibraryForDowngradeQueryHandler _handler;

    public GetLibraryForDowngradeQueryHandlerTests()
    {
        _repoMock = new Mock<IUserLibraryRepository>();
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _handler = new GetLibraryForDowngradeQueryHandler(_repoMock.Object, _db);
    }

    [Fact]
    public async Task Handle_NewQuotaLessThanTotal_SplitsIntoKeepAndRemove()
    {
        var userId = Guid.NewGuid();
        var entries = new List<UserLibraryEntry>
        {
            new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid()),
            new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid()),
            new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid()),
        };

        _repoMock
            .Setup(r => r.GetUserGamesAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _handler.Handle(
            new GetLibraryForDowngradeQuery(userId, NewQuota: 2),
            TestContext.Current.CancellationToken);

        result.GamesToKeep.Should().HaveCount(2);
        result.GamesToRemove.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_FavoriteGameSortedFirst()
    {
        var userId = Guid.NewGuid();

        var nonFavorite = new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid());
        var favorite = new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid());
        favorite.ToggleFavorite(); // sets IsFavorite = true

        var entries = new List<UserLibraryEntry> { nonFavorite, favorite };

        _repoMock
            .Setup(r => r.GetUserGamesAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _handler.Handle(
            new GetLibraryForDowngradeQuery(userId, NewQuota: 1),
            TestContext.Current.CancellationToken);

        result.GamesToKeep.Should().HaveCount(1);
        result.GamesToKeep[0].IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NewQuotaEqualsTotal_AllInKeep()
    {
        var userId = Guid.NewGuid();
        var entries = new List<UserLibraryEntry>
        {
            new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid()),
            new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid()),
        };

        _repoMock
            .Setup(r => r.GetUserGamesAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _handler.Handle(
            new GetLibraryForDowngradeQuery(userId, NewQuota: 2),
            TestContext.Current.CancellationToken);

        result.GamesToKeep.Should().HaveCount(2);
        result.GamesToRemove.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NewQuotaZero_AllInRemove()
    {
        var userId = Guid.NewGuid();
        var entries = new List<UserLibraryEntry>
        {
            new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid()),
            new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid()),
        };

        _repoMock
            .Setup(r => r.GetUserGamesAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _handler.Handle(
            new GetLibraryForDowngradeQuery(userId, NewQuota: 0),
            TestContext.Current.CancellationToken);

        result.GamesToKeep.Should().BeEmpty();
        result.GamesToRemove.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_EmptyLibrary_ReturnsBothEmpty()
    {
        var userId = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetUserGamesAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserLibraryEntry>());

        var result = await _handler.Handle(
            new GetLibraryForDowngradeQuery(userId, NewQuota: 5),
            TestContext.Current.CancellationToken);

        result.GamesToKeep.Should().BeEmpty();
        result.GamesToRemove.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NullQueryThrows()
    {
        var act = async () => await _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_GameTitleFallsBackToUnknownWhenNotInCatalog()
    {
        var userId = Guid.NewGuid();
        var entry = new UserLibraryEntry(Guid.NewGuid(), userId, Guid.NewGuid()); // GameId not in _db

        _repoMock
            .Setup(r => r.GetUserGamesAsync(userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserLibraryEntry> { entry });

        var result = await _handler.Handle(
            new GetLibraryForDowngradeQuery(userId, NewQuota: 1),
            TestContext.Current.CancellationToken);

        result.GamesToKeep.Should().HaveCount(1);
        result.GamesToKeep[0].GameTitle.Should().Be("Unknown Game");
        result.GamesToKeep[0].GameImageUrl.Should().BeNull();
    }
}
