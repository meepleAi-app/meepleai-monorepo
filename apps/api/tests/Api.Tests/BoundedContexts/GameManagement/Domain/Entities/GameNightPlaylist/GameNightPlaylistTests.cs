using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;

[Trait("Category", TestCategories.Unit)]
public class GameNightPlaylistTests
{
    private readonly Guid _userId = Guid.NewGuid();

    #region Create

    [Fact]
    public void Create_WithValidName_CreatesPlaylist()
    {
        var playlist = Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist.GameNightPlaylist
            .Create("Friday Night Games", _userId);

        Assert.NotEqual(Guid.Empty, playlist.Id);
        Assert.Equal("Friday Night Games", playlist.Name);
        Assert.Equal(_userId, playlist.CreatorUserId);
        Assert.Null(playlist.ScheduledDate);
        Assert.False(playlist.IsShared);
        Assert.Null(playlist.ShareToken);
        Assert.Empty(playlist.Games);
        Assert.False(playlist.IsDeleted);
        Assert.Null(playlist.DeletedAt);
    }

    [Fact]
    public void Create_WithScheduledDate_SetsDate()
    {
        var date = new DateTime(2026, 3, 15, 19, 0, 0, DateTimeKind.Utc);
        var playlist = Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist.GameNightPlaylist
            .Create("Game Night", _userId, date);

        Assert.Equal(date, playlist.ScheduledDate);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist.GameNightPlaylist
                .Create("", _userId));
    }

    [Fact]
    public void Create_WithNameExceeding200Chars_ThrowsArgumentException()
    {
        var longName = new string('A', 201);
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist.GameNightPlaylist
                .Create(longName, _userId));
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist.GameNightPlaylist
                .Create("Test", Guid.Empty));
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var playlist = Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist.GameNightPlaylist
            .Create("  Trimmed Name  ", _userId);

        Assert.Equal("Trimmed Name", playlist.Name);
    }

    [Fact]
    public void Create_RaisesDomainEvent()
    {
        var playlist = Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist.GameNightPlaylist
            .Create("Test", _userId);

        Assert.Single(playlist.DomainEvents);
    }

    #endregion

    #region UpdateName

    [Fact]
    public void UpdateName_WithValidName_UpdatesName()
    {
        var playlist = CreatePlaylist();
        playlist.UpdateName("New Name");

        Assert.Equal("New Name", playlist.Name);
    }

    [Fact]
    public void UpdateName_WithEmptyName_ThrowsArgumentException()
    {
        var playlist = CreatePlaylist();
        Assert.Throws<ArgumentException>(() => playlist.UpdateName(""));
    }

    [Fact]
    public void UpdateName_WithLongName_ThrowsArgumentException()
    {
        var playlist = CreatePlaylist();
        Assert.Throws<ArgumentException>(() => playlist.UpdateName(new string('A', 201)));
    }

    #endregion

    #region UpdateScheduledDate

    [Fact]
    public void UpdateScheduledDate_SetsDate()
    {
        var playlist = CreatePlaylist();
        var date = DateTime.UtcNow.AddDays(7);
        playlist.UpdateScheduledDate(date);

        Assert.Equal(date, playlist.ScheduledDate);
    }

    [Fact]
    public void UpdateScheduledDate_WithNull_ClearsDate()
    {
        var playlist = CreatePlaylist();
        playlist.UpdateScheduledDate(DateTime.UtcNow);
        playlist.UpdateScheduledDate(null);

        Assert.Null(playlist.ScheduledDate);
    }

    #endregion

    #region AddGame

    [Fact]
    public void AddGame_AddsGameAtPosition()
    {
        var playlist = CreatePlaylist();
        var gameId = Guid.NewGuid();

        playlist.AddGame(gameId, 1);

        Assert.Single(playlist.Games);
        Assert.Equal(gameId, playlist.Games[0].SharedGameId);
        Assert.Equal(1, playlist.Games[0].Position);
    }

    [Fact]
    public void AddGame_MultipleGames_MaintainsOrder()
    {
        var playlist = CreatePlaylist();
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var gameId3 = Guid.NewGuid();

        playlist.AddGame(gameId1, 1);
        playlist.AddGame(gameId2, 2);
        playlist.AddGame(gameId3, 3);

        Assert.Equal(3, playlist.Games.Count);
    }

    [Fact]
    public void AddGame_AtExistingPosition_ShiftsOthers()
    {
        var playlist = CreatePlaylist();
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();

        playlist.AddGame(gameId1, 1);
        playlist.AddGame(gameId2, 1); // Insert at position 1

        var game1 = playlist.Games.First(g => g.SharedGameId == gameId1);
        var game2 = playlist.Games.First(g => g.SharedGameId == gameId2);

        Assert.Equal(2, game1.Position); // Shifted
        Assert.Equal(1, game2.Position); // New game at 1
    }

    [Fact]
    public void AddGame_DuplicateGame_ThrowsConflictException()
    {
        var playlist = CreatePlaylist();
        var gameId = Guid.NewGuid();

        playlist.AddGame(gameId, 1);

        Assert.Throws<ConflictException>(() => playlist.AddGame(gameId, 2));
    }

    [Fact]
    public void AddGame_EmptyGameId_ThrowsArgumentException()
    {
        var playlist = CreatePlaylist();
        Assert.Throws<ArgumentException>(() => playlist.AddGame(Guid.Empty, 1));
    }

    [Fact]
    public void AddGame_ZeroPosition_ThrowsArgumentException()
    {
        var playlist = CreatePlaylist();
        Assert.Throws<ArgumentException>(() => playlist.AddGame(Guid.NewGuid(), 0));
    }

    #endregion

    #region RemoveGame

    [Fact]
    public void RemoveGame_RemovesAndReorders()
    {
        var playlist = CreatePlaylist();
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var gameId3 = Guid.NewGuid();

        playlist.AddGame(gameId1, 1);
        playlist.AddGame(gameId2, 2);
        playlist.AddGame(gameId3, 3);

        playlist.RemoveGame(gameId2);

        Assert.Equal(2, playlist.Games.Count);
        var game3 = playlist.Games.First(g => g.SharedGameId == gameId3);
        Assert.Equal(2, game3.Position); // Shifted down
    }

    [Fact]
    public void RemoveGame_NonExistent_ThrowsNotFoundException()
    {
        var playlist = CreatePlaylist();
        Assert.Throws<NotFoundException>(() => playlist.RemoveGame(Guid.NewGuid()));
    }

    [Fact]
    public void RemoveGame_EmptyId_ThrowsArgumentException()
    {
        var playlist = CreatePlaylist();
        Assert.Throws<ArgumentException>(() => playlist.RemoveGame(Guid.Empty));
    }

    #endregion

    #region ReorderGames

    [Fact]
    public void ReorderGames_ReordersCorrectly()
    {
        var playlist = CreatePlaylist();
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();
        var gameId3 = Guid.NewGuid();

        playlist.AddGame(gameId1, 1);
        playlist.AddGame(gameId2, 2);
        playlist.AddGame(gameId3, 3);

        playlist.ReorderGames(new List<Guid> { gameId3, gameId1, gameId2 });

        var g1 = playlist.Games.First(g => g.SharedGameId == gameId1);
        var g2 = playlist.Games.First(g => g.SharedGameId == gameId2);
        var g3 = playlist.Games.First(g => g.SharedGameId == gameId3);

        Assert.Equal(2, g1.Position);
        Assert.Equal(3, g2.Position);
        Assert.Equal(1, g3.Position);
    }

    [Fact]
    public void ReorderGames_WrongCount_ThrowsArgumentException()
    {
        var playlist = CreatePlaylist();
        playlist.AddGame(Guid.NewGuid(), 1);
        playlist.AddGame(Guid.NewGuid(), 2);

        Assert.Throws<ArgumentException>(() =>
            playlist.ReorderGames(new List<Guid> { Guid.NewGuid() }));
    }

    [Fact]
    public void ReorderGames_DifferentIds_ThrowsArgumentException()
    {
        var playlist = CreatePlaylist();
        var gameId1 = Guid.NewGuid();
        playlist.AddGame(gameId1, 1);

        Assert.Throws<ArgumentException>(() =>
            playlist.ReorderGames(new List<Guid> { Guid.NewGuid() }));
    }

    [Fact]
    public void ReorderGames_Null_ThrowsArgumentNullException()
    {
        var playlist = CreatePlaylist();
        Assert.Throws<ArgumentNullException>(() => playlist.ReorderGames(null!));
    }

    #endregion

    #region Share Token

    [Fact]
    public void GenerateShareToken_SetsTokenAndIsShared()
    {
        var playlist = CreatePlaylist();
        var token = playlist.GenerateShareToken();

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.Equal(token, playlist.ShareToken);
        Assert.True(playlist.IsShared);
    }

    [Fact]
    public void GenerateShareToken_CalledTwice_ReplacesToken()
    {
        var playlist = CreatePlaylist();
        var token1 = playlist.GenerateShareToken();
        var token2 = playlist.GenerateShareToken();

        Assert.NotEqual(token1, token2);
        Assert.Equal(token2, playlist.ShareToken);
    }

    [Fact]
    public void RevokeShareToken_ClearsTokenAndIsShared()
    {
        var playlist = CreatePlaylist();
        playlist.GenerateShareToken();
        playlist.RevokeShareToken();

        Assert.Null(playlist.ShareToken);
        Assert.False(playlist.IsShared);
    }

    #endregion

    #region SoftDelete

    [Fact]
    public void SoftDelete_MarksAsDeleted()
    {
        var playlist = CreatePlaylist();
        playlist.SoftDelete();

        Assert.True(playlist.IsDeleted);
        Assert.NotNull(playlist.DeletedAt);
    }

    [Fact]
    public void SoftDelete_AlreadyDeleted_ThrowsConflictException()
    {
        var playlist = CreatePlaylist();
        playlist.SoftDelete();

        Assert.Throws<ConflictException>(() => playlist.SoftDelete());
    }

    #endregion

    #region RestoreGames

    [Fact]
    public void RestoreGames_RestoresFromList()
    {
        var playlist = CreatePlaylist();
        var games = new List<PlaylistGame>
        {
            new() { SharedGameId = Guid.NewGuid(), Position = 1, AddedAt = DateTime.UtcNow },
            new() { SharedGameId = Guid.NewGuid(), Position = 2, AddedAt = DateTime.UtcNow }
        };

        playlist.RestoreGames(games);

        Assert.Equal(2, playlist.Games.Count);
    }

    #endregion

    private Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist.GameNightPlaylist CreatePlaylist()
    {
        return Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist.GameNightPlaylist
            .Create("Test Playlist", _userId);
    }
}
