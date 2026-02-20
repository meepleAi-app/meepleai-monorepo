using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveSessionPlayerTests
{
    private LiveSessionPlayer CreatePlayer(
        string displayName = "Marco",
        PlayerColor color = PlayerColor.Red,
        PlayerRole role = PlayerRole.Player,
        Guid? userId = null)
    {
        return new LiveSessionPlayer(
            Guid.NewGuid(),
            Guid.NewGuid(),
            userId,
            displayName,
            color,
            role,
            DateTime.UtcNow);
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesSuccessfully()
    {
        var userId = Guid.NewGuid();
        var player = CreatePlayer(userId: userId);

        Assert.Equal("Marco", player.DisplayName);
        Assert.Equal(PlayerColor.Red, player.Color);
        Assert.Equal(PlayerRole.Player, player.Role);
        Assert.Equal(userId, player.UserId);
        Assert.Equal(0, player.TotalScore);
        Assert.Equal(0, player.CurrentRank);
        Assert.True(player.IsActive);
        Assert.Null(player.TeamId);
    }

    [Fact]
    public void Constructor_GuestPlayer_NoUserId()
    {
        var player = CreatePlayer(userId: null);
        Assert.Null(player.UserId);
    }

    [Fact]
    public void Constructor_EmptyPlayerId_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new LiveSessionPlayer(Guid.Empty, Guid.NewGuid(), null, "Test", PlayerColor.Red, PlayerRole.Player, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_EmptySessionId_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new LiveSessionPlayer(Guid.NewGuid(), Guid.Empty, null, "Test", PlayerColor.Red, PlayerRole.Player, DateTime.UtcNow));
    }

    [Fact]
    public void Constructor_EmptyDisplayName_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() => CreatePlayer(displayName: ""));
    }

    [Fact]
    public void Constructor_DisplayNameTooLong_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() => CreatePlayer(displayName: new string('x', 101)));
    }

    [Fact]
    public void Constructor_TrimsDisplayName()
    {
        var player = CreatePlayer(displayName: "  Marco  ");
        Assert.Equal("Marco", player.DisplayName);
    }

    [Fact]
    public void UpdateScore_SetsValues()
    {
        var player = CreatePlayer();
        player.UpdateScore(42, 1);

        Assert.Equal(42, player.TotalScore);
        Assert.Equal(1, player.CurrentRank);
    }

    [Fact]
    public void AssignToTeam_SetsTeamId()
    {
        var player = CreatePlayer();
        var teamId = Guid.NewGuid();

        player.AssignToTeam(teamId);

        Assert.Equal(teamId, player.TeamId);
    }

    [Fact]
    public void AssignToTeam_Null_RemovesFromTeam()
    {
        var player = CreatePlayer();
        player.AssignToTeam(Guid.NewGuid());
        player.AssignToTeam(null);

        Assert.Null(player.TeamId);
    }

    [Fact]
    public void ChangeRole_UpdatesRole()
    {
        var player = CreatePlayer(role: PlayerRole.Player);
        player.ChangeRole(PlayerRole.Host);

        Assert.Equal(PlayerRole.Host, player.Role);
    }

    [Fact]
    public void Deactivate_SetsInactive()
    {
        var player = CreatePlayer();
        player.Deactivate();

        Assert.False(player.IsActive);
    }

    [Fact]
    public void Activate_SetsActive()
    {
        var player = CreatePlayer();
        player.Deactivate();
        player.Activate();

        Assert.True(player.IsActive);
    }

    [Fact]
    public void UpdateDisplayName_ValidName_Updates()
    {
        var player = CreatePlayer();
        player.UpdateDisplayName("Luca");

        Assert.Equal("Luca", player.DisplayName);
    }

    [Fact]
    public void UpdateDisplayName_Empty_ThrowsValidationException()
    {
        var player = CreatePlayer();
        Assert.Throws<ValidationException>(() => player.UpdateDisplayName(""));
    }

    [Fact]
    public void UpdateColor_ChangesColor()
    {
        var player = CreatePlayer(color: PlayerColor.Red);
        player.UpdateColor(PlayerColor.Blue);

        Assert.Equal(PlayerColor.Blue, player.Color);
    }
}
