using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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

        team.Name.Should().Be("Alpha");
        team.Color.Should().Be("#FF0000");
        team.TeamScore.Should().Be(0);
        team.CurrentRank.Should().Be(0);
        team.PlayerIds.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_EmptyTeamId_ThrowsValidationException()
    {
        var act = () =>
            new LiveSessionTeam(Guid.Empty, Guid.NewGuid(), "Alpha", "#FF0000");
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_EmptySessionId_ThrowsValidationException()
    {
        var act = () =>
            new LiveSessionTeam(Guid.NewGuid(), Guid.Empty, "Alpha", "#FF0000");
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_EmptyName_ThrowsValidationException()
    {
        ((Action)(() => CreateTeam(name: ""))).Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_NameTooLong_ThrowsValidationException()
    {
        ((Action)(() => CreateTeam(name: new string('x', 51)))).Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_EmptyColor_ThrowsValidationException()
    {
        ((Action)(() => CreateTeam(color: ""))).Should().Throw<ValidationException>();
    }

    [Fact]
    public void Constructor_TrimsNameAndColor()
    {
        var team = CreateTeam(name: "  Alpha  ", color: "  #FF0000  ");
        team.Name.Should().Be("Alpha");
        team.Color.Should().Be("#FF0000");
    }

    [Fact]
    public void AddPlayer_ValidId_AddsToList()
    {
        var team = CreateTeam();
        var playerId = Guid.NewGuid();

        team.AddPlayer(playerId);

        team.PlayerIds.Should().ContainSingle();
        team.PlayerIds[0].Should().Be(playerId);
    }

    [Fact]
    public void AddPlayer_EmptyId_ThrowsValidationException()
    {
        var team = CreateTeam();
        ((Action)(() => team.AddPlayer(Guid.Empty))).Should().Throw<ValidationException>();
    }

    [Fact]
    public void AddPlayer_DuplicateId_ThrowsDomainException()
    {
        var team = CreateTeam();
        var playerId = Guid.NewGuid();
        team.AddPlayer(playerId);

        ((Action)(() => team.AddPlayer(playerId))).Should().Throw<DomainException>();
    }

    [Fact]
    public void RemovePlayer_ExistingId_RemovesFromList()
    {
        var team = CreateTeam();
        var playerId = Guid.NewGuid();
        team.AddPlayer(playerId);

        team.RemovePlayer(playerId);

        team.PlayerIds.Should().BeEmpty();
    }

    [Fact]
    public void RemovePlayer_NonExistentId_ThrowsDomainException()
    {
        var team = CreateTeam();
        ((Action)(() => team.RemovePlayer(Guid.NewGuid()))).Should().Throw<DomainException>();
    }

    [Fact]
    public void UpdateScore_SetsValues()
    {
        var team = CreateTeam();
        team.UpdateScore(100, 1);

        team.TeamScore.Should().Be(100);
        team.CurrentRank.Should().Be(1);
    }

    [Fact]
    public void UpdateName_ValidName_Updates()
    {
        var team = CreateTeam();
        team.UpdateName("Beta");

        team.Name.Should().Be("Beta");
    }

    [Fact]
    public void UpdateName_Empty_ThrowsValidationException()
    {
        var team = CreateTeam();
        ((Action)(() => team.UpdateName(""))).Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdateName_TooLong_ThrowsValidationException()
    {
        var team = CreateTeam();
        ((Action)(() => team.UpdateName(new string('x', 51)))).Should().Throw<ValidationException>();
    }
}
