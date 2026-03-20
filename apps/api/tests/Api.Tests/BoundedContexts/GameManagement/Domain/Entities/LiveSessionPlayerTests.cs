using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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

        player.DisplayName.Should().Be("Marco");
        player.Color.Should().Be(PlayerColor.Red);
        player.Role.Should().Be(PlayerRole.Player);
        player.UserId.Should().Be(userId);
        player.TotalScore.Should().Be(0);
        player.CurrentRank.Should().Be(0);
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
        ((Action)(() => CreatePlayer(displayName: ""))).Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_DisplayNameTooLong_ThrowsValidationException()
    {
        ((Action)(() => CreatePlayer(displayName: new string('x', 101)))).Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_TrimsDisplayName()
    {
        var player = CreatePlayer(displayName: "  Marco  ");
        player.DisplayName.Should().Be("Marco");
    }

    [Fact]
    public void UpdateScore_SetsValues()
    {
        var player = CreatePlayer();
        player.UpdateScore(42, 1);

        player.TotalScore.Should().Be(42);
        player.CurrentRank.Should().Be(1);
    }

    [Fact]
    public void AssignToTeam_SetsTeamId()
    {
        var player = CreatePlayer();
        var teamId = Guid.NewGuid();

        player.AssignToTeam(teamId);

        player.TeamId.Should().Be(teamId);
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

        player.Role.Should().Be(PlayerRole.Host);
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

        player.DisplayName.Should().Be("Luca");
    }

    [Fact]
    public void UpdateDisplayName_Empty_ThrowsValidationException()
    {
        var player = CreatePlayer();
        ((Action)(() => player.UpdateDisplayName(""))).Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdateColor_ChangesColor()
    {
        var player = CreatePlayer(color: PlayerColor.Red);
        player.UpdateColor(PlayerColor.Blue);

        player.Color.Should().Be(PlayerColor.Blue);
    }
}
