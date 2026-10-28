using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// #4692: <see cref="GameNightEvent.Create"/> can take the identity from the caller, so that the same
/// creation sent again is recognisably the same one.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightEventCreateIdTests
{
    private static readonly DateTimeOffset Later = DateTimeOffset.UtcNow.AddDays(7);

    [Fact]
    public void Create_WithAnId_UsesIt()
    {
        var id = Guid.NewGuid();

        var gameNight = GameNightEvent.Create(Guid.NewGuid(), "Friday Night Catan", Later, id: id);

        gameNight.Id.Should().Be(id);
    }

    [Fact]
    public void Create_WithoutAnId_GeneratesADifferentOneEachTime()
    {
        var first = GameNightEvent.Create(Guid.NewGuid(), "Friday Night Catan", Later);
        var second = GameNightEvent.Create(Guid.NewGuid(), "Friday Night Catan", Later);

        first.Id.Should().NotBe(Guid.Empty);
        second.Id.Should().NotBe(Guid.Empty);
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Create_WithTheEmptyGuid_Throws()
    {
        var act = () => GameNightEvent.Create(Guid.NewGuid(), "Friday Night Catan", Later, id: Guid.Empty);

        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }
}
