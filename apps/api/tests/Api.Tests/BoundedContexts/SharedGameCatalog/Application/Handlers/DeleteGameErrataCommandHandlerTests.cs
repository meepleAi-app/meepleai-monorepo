using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class DeleteGameErrataCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<DeleteGameErrataCommandHandler>> _loggerMock;
    private readonly DetailCacheEvictionDoubles _eviction = new();
    private readonly DeleteGameErrataCommandHandler _handler;

    public DeleteGameErrataCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<DeleteGameErrataCommandHandler>>();
        _handler = new DeleteGameErrataCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _eviction.Cache.Object,
            _eviction.RetryPolicy.Object,
            _eviction.HybridCache.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_DeletesErrataSuccessfully()
    {
        // Arrange
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var errata = GameErrata.Create(game.Id, "Description", "Page 5", DateTime.UtcNow);
        game.AddErrata(errata);

        var command = new DeleteGameErrataCommand(SharedGameId: game.Id, ErrataId: errata.Id);

        _repositoryMock
            .Setup(r => r.GetGameByErrataIdAsync(errata.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        _repositoryMock.Verify(r => r.Update(game), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentErrata_ThrowsNotFoundException()
    {
        // Arrange
        var errataId = Guid.NewGuid();
        var command = new DeleteGameErrataCommand(SharedGameId: Guid.NewGuid(), ErrataId: errataId);

        _repositoryMock
            .Setup(r => r.GetGameByErrataIdAsync(errataId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _repositoryMock.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithErrataOfAnotherGame_ThrowsNotFoundException()
    {
        // #4543: il gioco della rotta non è quello dell'errata.
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);
        var errata = GameErrata.Create(game.Id, "Description", "Page 5", DateTime.UtcNow);
        game.AddErrata(errata);

        _repositoryMock
            .Setup(r => r.GetGameByErrataIdAsync(errata.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        var command = new DeleteGameErrataCommand(SharedGameId: Guid.NewGuid(), ErrataId: errata.Id);

        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        game.Erratas.Should().ContainSingle("the erratum of another game must not be removed");
        _repositoryMock.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// #4565: il dettaglio del gioco riporta le errata e sta in cache. La prova end-to-end è
    /// <c>Detail_AfterDeletingAnErratum_NoLongerReportsIt</c>, su Postgres.
    /// </summary>
    [Fact]
    public async Task Handle_AfterSave_EvictsTheDetailCacheOfTheGame()
    {
        var game = DetailCacheEvictionDoubles.NewGame();
        var errata = GameErrata.Create(game.Id, "Description", "Page 5", DateTime.UtcNow);
        game.AddErrata(errata);
        _repositoryMock
            .Setup(r => r.GetGameByErrataIdAsync(errata.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(_eviction.MarkWritten)
            .ReturnsAsync(1);

        await _handler.Handle(
            new DeleteGameErrataCommand(game.Id, errata.Id),
            TestContext.Current.CancellationToken);

        _eviction.AssertEvictedAfterTheWrite(game.Id);
    }

    [Fact]
    public async Task Handle_WithAnErratumOfAnotherGame_EvictsNothing()
    {
        var owner = DetailCacheEvictionDoubles.NewGame();
        var errata = GameErrata.Create(owner.Id, "Of another game", "Page 1", DateTime.UtcNow);
        owner.AddErrata(errata);
        _repositoryMock
            .Setup(r => r.GetGameByErrataIdAsync(errata.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);

        var act = () => _handler.Handle(
            new DeleteGameErrataCommand(Guid.NewGuid(), errata.Id),
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NotFoundException>();

        _eviction.AssertNothingEvicted();
    }
}
