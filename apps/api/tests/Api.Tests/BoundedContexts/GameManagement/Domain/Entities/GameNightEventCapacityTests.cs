using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// #4921: the seats. <c>IsFull</c> counts the RSVP rows only, because the guests invited by email are
/// another aggregate; a decision about a seat passes their accepted count in through
/// <c>IsFullWith</c>. Four seats, one accepted member, three accepted guests: full.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4921")]
public sealed class GameNightEventCapacityTests
{
    private static GameNightEvent NightWith(int? maxPlayers, int acceptedMembers, int pendingMembers = 0)
    {
        var members = Enumerable.Range(0, acceptedMembers + pendingMembers).Select(_ => Guid.NewGuid()).ToList();
        var night = GameNightEvent.Create(
            Guid.NewGuid(), "Serata", DateTimeOffset.UtcNow.AddDays(1), gameIds: [Guid.NewGuid()], maxPlayers: maxPlayers);
        night.Publish(members);
        foreach (var member in members.Take(acceptedMembers))
            night.GetRsvp(member)!.Accept();
        return night;
    }

    [Fact]
    public void IsFullWith_CountsTheAcceptedGuestsAlongsideTheAcceptedMembers()
    {
        var night = NightWith(maxPlayers: 4, acceptedMembers: 1, pendingMembers: 1);

        night.IsFull.Should().BeFalse("the rows alone say one of four");
        night.IsFullWith(2).Should().BeFalse("three of four");
        night.IsFullWith(3).Should().BeTrue("four of four");
        night.IsFullWith(5).Should().BeTrue("over is full too");
    }

    [Fact]
    public void IsFullWith_PendingMembers_DoNotTakeASeat()
    {
        var night = NightWith(maxPlayers: 2, acceptedMembers: 0, pendingMembers: 5);

        night.IsFullWith(1).Should().BeFalse();
        night.IsFullWith(2).Should().BeTrue();
    }

    [Fact]
    public void IsFullWith_NoSeatCap_IsNeverFull()
    {
        NightWith(maxPlayers: null, acceptedMembers: 3).IsFullWith(100).Should().BeFalse();
    }

    [Fact]
    public void IsFull_IsIsFullWithZero()
    {
        // The smallest table the aggregate admits is two seats.
        var night = NightWith(maxPlayers: 2, acceptedMembers: 2);

        night.IsFull.Should().BeTrue();
        night.IsFull.Should().Be(night.IsFullWith(0));
    }

    [Fact]
    public void IsFullWith_RefusesANegativeCount()
    {
        var act = () => NightWith(maxPlayers: 4, acceptedMembers: 0).IsFullWith(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
