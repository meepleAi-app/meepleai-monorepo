using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class LiveSessionTeamTests
{
    private LiveSessionTeam CreateTeam(
        string name = "Alpha",
        string color = "#FF0000")
    {
        return new LiveSessionTeam(Guid.NewGuid(), Guid.NewGuid(), name, color);
    }

    [Fact]
    public void Constructor_ValidParameters_CreatesSuccessfully()
    {
        var team = CreateTeam();

        Assert.Equal("Alpha", team.Name);
        Assert.Equal("#FF0000", team.Color);
        Assert.Equal(0, team.TeamScore);
        Assert.Equal(0, team.CurrentRank);
        Assert.Empty(team.PlayerIds);
    }

    [Fact]
    public void Constructor_EmptyTeamId_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new LiveSessionTeam(Guid.Empty, Guid.NewGuid(), "Alpha", "#FF0000"));
    }

    [Fact]
    public void Constructor_EmptySessionId_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() =>
            new LiveSessionTeam(Guid.NewGuid(), Guid.Empty, "Alpha", "#FF0000"));
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() => CreateTeam(name: ""));
    }

    [Fact]
    public void Constructor_NameTooLong_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() => CreateTeam(name: new string('x', 51)));
    }

    [Fact]
    public void Constructor_EmptyColor_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() => CreateTeam(color: ""));
    }

    [Fact]
    public void Constructor_TrimsNameAndColor()
    {
        var team = CreateTeam(name: "  Alpha  ", color: "  #FF0000  ");
        Assert.Equal("Alpha", team.Name);
        Assert.Equal("#FF0000", team.Color);
    }

    [Fact]
    public void AddPlayer_ValidId_AddsToList()
    {
        var team = CreateTeam();
        var playerId = Guid.NewGuid();

        team.AddPlayer(playerId);

        Assert.Single(team.PlayerIds);
        Assert.Equal(playerId, team.PlayerIds[0]);
    }

    [Fact]
    public void AddPlayer_EmptyId_ThrowsValidationException()
    {
        var team = CreateTeam();
        Assert.Throws<ValidationException>(() => team.AddPlayer(Guid.Empty));
    }

    [Fact]
    public void AddPlayer_DuplicateId_ThrowsDomainException()
    {
        var team = CreateTeam();
        var playerId = Guid.NewGuid();
        team.AddPlayer(playerId);

        Assert.Throws<DomainException>(() => team.AddPlayer(playerId));
    }

    [Fact]
    public void RemovePlayer_ExistingId_RemovesFromList()
    {
        var team = CreateTeam();
        var playerId = Guid.NewGuid();
        team.AddPlayer(playerId);

        team.RemovePlayer(playerId);

        Assert.Empty(team.PlayerIds);
    }

    [Fact]
    public void RemovePlayer_NonExistentId_ThrowsDomainException()
    {
        var team = CreateTeam();
        Assert.Throws<DomainException>(() => team.RemovePlayer(Guid.NewGuid()));
    }

    [Fact]
    public void UpdateScore_SetsValues()
    {
        var team = CreateTeam();
        team.UpdateScore(100, 1);

        Assert.Equal(100, team.TeamScore);
        Assert.Equal(1, team.CurrentRank);
    }

    [Fact]
    public void UpdateName_ValidName_Updates()
    {
        var team = CreateTeam();
        team.UpdateName("Beta");

        Assert.Equal("Beta", team.Name);
    }

    [Fact]
    public void UpdateName_Empty_ThrowsValidationException()
    {
        var team = CreateTeam();
        Assert.Throws<ValidationException>(() => team.UpdateName(""));
    }

    [Fact]
    public void UpdateName_TooLong_ThrowsValidationException()
    {
        var team = CreateTeam();
        Assert.Throws<ValidationException>(() => team.UpdateName(new string('x', 51)));
    }
}
