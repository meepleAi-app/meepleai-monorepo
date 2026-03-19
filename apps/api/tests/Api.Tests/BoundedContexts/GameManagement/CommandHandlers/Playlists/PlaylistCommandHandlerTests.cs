using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.CommandHandlers.Playlists;

[Trait("Category", TestCategories.Unit)]
public class PlaylistCommandHandlerTests
{
    private readonly Mock<IGameNightPlaylistRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Guid _userId = Guid.NewGuid();

    public PlaylistCommandHandlerTests()
    {
        _repositoryMock = new Mock<IGameNightPlaylistRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
    }

    #region CreatePlaylistCommandHandler

    [Fact]
    public async Task CreatePlaylist_ReturnsDto_WithCorrectData()
    {
        var handler = new CreatePlaylistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new CreatePlaylistCommand("Friday Night Games", _userId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Friday Night Games", result.Name);
        Assert.Equal(_userId, result.CreatorUserId);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.False(result.IsShared);
        Assert.Empty(result.Games);

        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<GameNightPlaylist>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlaylist_WithScheduledDate_SetsDate()
    {
        var handler = new CreatePlaylistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var date = new DateTime(2026, 3, 20, 19, 0, 0, DateTimeKind.Utc);
        var command = new CreatePlaylistCommand("Game Night", _userId, date);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(date, result.ScheduledDate);
    }

    #endregion

    #region UpdatePlaylistCommandHandler

    [Fact]
    public async Task UpdatePlaylist_UpdatesName()
    {
        var playlist = GameNightPlaylist.Create("Old Name", _userId);
        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new UpdatePlaylistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new UpdatePlaylistCommand(playlist.Id, _userId, "New Name");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("New Name", result.Name);
        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<GameNightPlaylist>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdatePlaylist_NotFound_ThrowsNotFoundException()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightPlaylist?)null);

        var handler = new UpdatePlaylistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new UpdatePlaylistCommand(Guid.NewGuid(), _userId, "Name");

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task UpdatePlaylist_WrongUser_ThrowsUnauthorized()
    {
        var playlist = GameNightPlaylist.Create("Name", Guid.NewGuid());
        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new UpdatePlaylistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new UpdatePlaylistCommand(playlist.Id, _userId, "Name");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
    }

    #endregion

    #region DeletePlaylistCommandHandler

    [Fact]
    public async Task DeletePlaylist_SoftDeletes()
    {
        var playlist = GameNightPlaylist.Create("Name", _userId);
        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new DeletePlaylistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new DeletePlaylistCommand(playlist.Id, _userId);

        await handler.Handle(command, CancellationToken.None);

        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<GameNightPlaylist>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeletePlaylist_NotFound_ThrowsNotFoundException()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightPlaylist?)null);

        var handler = new DeletePlaylistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new DeletePlaylistCommand(Guid.NewGuid(), _userId);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command, CancellationToken.None));
    }

    #endregion

    #region AddGameToPlaylistCommandHandler

    [Fact]
    public async Task AddGame_AddsToPlaylist()
    {
        var playlist = GameNightPlaylist.Create("Name", _userId);
        var gameId = Guid.NewGuid();
        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new AddGameToPlaylistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new AddGameToPlaylistCommand(playlist.Id, _userId, gameId, 1);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Single(result.Games);
        Assert.Equal(gameId, result.Games[0].SharedGameId);
    }

    #endregion

    #region RemoveGameFromPlaylistCommandHandler

    [Fact]
    public async Task RemoveGame_RemovesFromPlaylist()
    {
        var playlist = GameNightPlaylist.Create("Name", _userId);
        var gameId = Guid.NewGuid();
        playlist.AddGame(gameId, 1);

        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new RemoveGameFromPlaylistCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new RemoveGameFromPlaylistCommand(playlist.Id, _userId, gameId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Empty(result.Games);
    }

    #endregion

    #region ReorderPlaylistGamesCommandHandler

    [Fact]
    public async Task ReorderGames_ReordersCorrectly()
    {
        var playlist = GameNightPlaylist.Create("Name", _userId);
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        playlist.AddGame(gameId1, 1);
        playlist.AddGame(gameId2, 2);

        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new ReorderPlaylistGamesCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new ReorderPlaylistGamesCommand(playlist.Id, _userId, new List<Guid> { gameId2, gameId1 });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, result.Games.Count);
        Assert.Equal(gameId2, result.Games[0].SharedGameId); // First by position
        Assert.Equal(gameId1, result.Games[1].SharedGameId); // Second by position
    }

    #endregion

    #region GenerateShareLinkCommandHandler

    [Fact]
    public async Task GenerateShareLink_ReturnsTokenAndUrl()
    {
        var playlist = GameNightPlaylist.Create("Name", _userId);
        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new GenerateShareLinkCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new GenerateShareLinkCommand(playlist.Id, _userId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result.ShareToken);
        Assert.NotEmpty(result.ShareToken);
        Assert.Contains(result.ShareToken, result.ShareUrl);
    }

    #endregion

    #region RevokeShareLinkCommandHandler

    [Fact]
    public async Task RevokeShareLink_RevokesToken()
    {
        var playlist = GameNightPlaylist.Create("Name", _userId);
        playlist.GenerateShareToken();

        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new RevokeShareLinkCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
        var command = new RevokeShareLinkCommand(playlist.Id, _userId);

        await handler.Handle(command, CancellationToken.None);

        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<GameNightPlaylist>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
