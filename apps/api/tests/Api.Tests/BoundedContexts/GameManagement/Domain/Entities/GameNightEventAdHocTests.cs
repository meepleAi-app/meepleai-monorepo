using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
public class GameNightEventAdHocTests
{
    [Fact]
    public void CreateAdHoc_SetsStatusInProgress_And_AddsFirstGame()
    {
        var organizerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var night = GameNightEvent.CreateAdHoc(organizerId, "Serata del 09/04", gameId);

        night.OrganizerId.Should().Be(organizerId);
        night.Status.Should().Be(GameNightStatus.InProgress);
        night.GameIds.Should().ContainSingle().Which.Should().Be(gameId);
        night.Title.Should().Be("Serata del 09/04");
        night.Rsvps.Should().BeEmpty();
    }

    [Fact]
    public void AttachAdditionalGame_WhenInProgress_AddsToGameIds()
    {
        var firstGame = Guid.NewGuid();
        var secondGame = Guid.NewGuid();
        var night = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Serata", firstGame);

        night.AttachAdditionalGame(secondGame);

        night.GameIds.Should().HaveCount(2).And.Contain(new[] { firstGame, secondGame });
    }

    [Fact]
    public void AttachAdditionalGame_WhenDraft_Throws()
    {
        var night = GameNightEvent.Create(
            Guid.NewGuid(),
            "Pianificata",
            DateTimeOffset.UtcNow.AddDays(1));

        var act = () => night.AttachAdditionalGame(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AttachAdditionalGame_DuplicateGameId_IsIdempotent()
    {
        var gameId = Guid.NewGuid();
        var night = GameNightEvent.CreateAdHoc(Guid.NewGuid(), "Serata", gameId);

        night.AttachAdditionalGame(gameId);

        night.GameIds.Should().ContainSingle().Which.Should().Be(gameId);
    }
}
