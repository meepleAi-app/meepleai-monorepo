using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// #4752: the name a player gets at the table when there is no usable one. It is shown to every player of
/// the game, so it must never be (or contain) the e-mail address — and the user store fills a missing
/// display name WITH the address, so «no usable name» has to include a name that is an e-mail.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class RosterDisplayNamesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("marta@example.com")]
    [InlineData("  Marta.Rossi@Example.COM  ")]
    [InlineData("a@b.c")]
    public void Resolve_NoUsableName_GivesTheFallback(string? name)
    {
        RosterDisplayNames.Resolve(name, RosterDisplayNames.Organizer).Should().Be("Organizzatore");
        RosterDisplayNames.Resolve(name, RosterDisplayNames.Player).Should().Be("Giocatore");
    }

    [Theory]
    [InlineData("Marta")]
    [InlineData("Marta Rossi")]
    [InlineData("Marta @ casa")]
    [InlineData("marta@work")]
    [InlineData("a@b@c.d")]
    [InlineData("@marta.rossi")]
    public void Resolve_AnyOtherName_IsKeptAsIs(string name)
    {
        RosterDisplayNames.Resolve(name, RosterDisplayNames.Organizer).Should().Be(name);
    }

    [Theory]
    [InlineData("marta@example.com", true)]
    [InlineData("x@y.z", true)]
    [InlineData("marta@example", false)]
    [InlineData("marta example.com", false)]
    [InlineData("a@b@c.d", false)]
    [InlineData("@example.com", false)]
    public void LooksLikeAnEmailAddress_IsTheShapeOfAnAddress(string value, bool expected)
    {
        RosterDisplayNames.LooksLikeAnEmailAddress(value).Should().Be(expected);
    }

    /// <summary>
    /// #5150 — every name the server seats on its own passes through here, and a profile name reaches 128
    /// characters: it leaves cut to the limit of a player.
    /// </summary>
    [Fact]
    [Trait("Issue", "5150")]
    public void Resolve_AProfileNameOverTheLimit_IsCutToTheLimitOfAPlayer()
    {
        RosterDisplayNames.Resolve(new string('m', 128), RosterDisplayNames.Player).Should().HaveLength(50);
    }

    [Fact]
    [Trait("Issue", "5150")]
    public void Clamp_DoesNotLeaveHalfOfAnEmoji()
    {
        // 49 letters, then an emoji whose second half would fall over the limit.
        Api.SharedKernel.Domain.Validation.PlayerNameLimit.Clamp(new string('a', 49) + "🎲" + "tail")
            .Should().Be(new string('a', 49));
    }
}
