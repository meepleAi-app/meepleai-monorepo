using Api.BoundedContexts.GameManagement.Application.Commands.Playlists;
using Api.BoundedContexts.GameManagement.Application.Queries.Playlists;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.QueryHandlers.Playlists;

[Trait("Category", TestCategories.Unit)]
public class PlaylistQueryHandlerTests
{
    private readonly Mock<IGameNightPlaylistRepository> _repositoryMock;
    private readonly Guid _userId = Guid.NewGuid();

    public PlaylistQueryHandlerTests()
    {
        _repositoryMock = new Mock<IGameNightPlaylistRepository>();
    }

    #region GetPlaylistQueryHandler

    [Fact]
    public async Task GetPlaylist_ReturnsDto()
    {
        var playlist = GameNightPlaylist.Create("Test", _userId);
        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new GetPlaylistQueryHandler(_repositoryMock.Object);
        var query = new GetPlaylistQuery(playlist.Id, _userId);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(playlist.Id, result.Id);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public async Task GetPlaylist_NotFound_ThrowsNotFoundException()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightPlaylist?)null);

        var handler = new GetPlaylistQueryHandler(_repositoryMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetPlaylistQuery(Guid.NewGuid(), _userId), CancellationToken.None));
    }

    [Fact]
    public async Task GetPlaylist_WrongUser_ThrowsUnauthorized()
    {
        var playlist = GameNightPlaylist.Create("Test", Guid.NewGuid());
        _repositoryMock.Setup(r => r.GetByIdAsync(playlist.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new GetPlaylistQueryHandler(_repositoryMock.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetPlaylistQuery(playlist.Id, _userId), CancellationToken.None));
    }

    #endregion

    #region GetPlaylistByShareTokenQueryHandler

    [Fact]
    public async Task GetPlaylistByShareToken_ReturnsDto()
    {
        var playlist = GameNightPlaylist.Create("Shared", _userId);
        playlist.GenerateShareToken();

        _repositoryMock.Setup(r => r.GetByShareTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(playlist);

        var handler = new GetPlaylistByShareTokenQueryHandler(_repositoryMock.Object);
        var query = new GetPlaylistByShareTokenQuery("some-token");

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal("Shared", result.Name);
    }

    [Fact]
    public async Task GetPlaylistByShareToken_NotFound_ThrowsNotFoundException()
    {
        _repositoryMock.Setup(r => r.GetByShareTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightPlaylist?)null);

        var handler = new GetPlaylistByShareTokenQueryHandler(_repositoryMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetPlaylistByShareTokenQuery("invalid"), CancellationToken.None));
    }

    #endregion

    #region GetUserPlaylistsQueryHandler

    [Fact]
    public async Task GetUserPlaylists_ReturnsPaginatedResponse()
    {
        var playlists = new List<GameNightPlaylist>
        {
            GameNightPlaylist.Create("Playlist 1", _userId),
            GameNightPlaylist.Create("Playlist 2", _userId)
        };

        _repositoryMock.Setup(r => r.GetByCreatorPaginatedAsync(_userId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((playlists as IReadOnlyList<GameNightPlaylist>, 2));

        var handler = new GetUserPlaylistsQueryHandler(_repositoryMock.Object);
        var query = new GetUserPlaylistsQuery(_userId, 1, 20);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Playlists.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task GetUserPlaylists_EmptyList_ReturnsEmptyResponse()
    {
        _repositoryMock.Setup(r => r.GetByCreatorPaginatedAsync(_userId, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<GameNightPlaylist>() as IReadOnlyList<GameNightPlaylist>, 0));

        var handler = new GetUserPlaylistsQueryHandler(_repositoryMock.Object);
        var query = new GetUserPlaylistsQuery(_userId, 1, 20);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Playlists);
        Assert.Equal(0, result.TotalPages);
    }

    #endregion
}
