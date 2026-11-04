using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// #4846. A guest of a game night is an address, not an account, so «stop writing to me» is recorded against the
/// address: a list the sends consult. The address is kept the way invitations keep it (trimmed, lower case), so a
/// guest invited as «Marco@Example.com» by one organizer and as «marco@example.com» by another is one entry.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4846")]
public sealed class GameNightGuestEmailOptOutTests
{
    private static readonly DateTimeOffset Now = new(2031, 5, 17, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("marco@example.com", "marco@example.com")]
    [InlineData("  Marco@Example.COM  ", "marco@example.com")]
    public void Create_KeepsTheAddressTrimmedAndInLowerCase(string given, string kept)
    {
        var optOut = GameNightGuestEmailOptOut.Create(given, Guid.NewGuid(), Now);

        optOut.Email.Should().Be(kept);
    }

    [Theory]
    [InlineData("Marco Rossi <Marco@Example.com>", "marco@example.com")]
    [InlineData("  <marco@example.com> ", "marco@example.com")]
    [InlineData("\"Rossi, Marco\" <marco@example.com>", "marco@example.com")]
    public void AnAddressWrittenWithAName_IsTheBareAddress_BecauseThatIsWhereTheTransportWrites(string given, string bare)
    {
        // «Marco <marco@x.com>» is mailed to marco@x.com. Compared as strings the two would be two addresses, and
        // an opt-out recorded for one would not stop the other (whoever invites with a name gets around the list,
        // and a guest invited with a name who opts out is not protected from the bare spelling).
        GameNightGuestEmailOptOut.Normalize(given).Should().Be(bare);
        GameNightGuestEmailOptOut.Create(given, Guid.NewGuid(), Now).Email.Should().Be(bare);
    }

    [Fact]
    public void WhatCannotBeParsedAsAnAddressWithAName_IsKeptAsItWas_TrimmedAndInLowerCase()
    {
        GameNightGuestEmailOptOut.Normalize("  Broken <Marco@Example.com ").Should().Be("broken <marco@example.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RefusesAnEmptyAddress(string? given)
    {
        var act = () => GameNightGuestEmailOptOut.Create(given!, Guid.NewGuid(), Now);

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Fact]
    public void Create_RefusesAnAddressLongerThanTheColumn()
    {
        var tooLong = new string('a', GameNightGuestEmailOptOut.MaxEmailLength - 11) + "@example.com";

        var act = () => GameNightGuestEmailOptOut.Create(tooLong, Guid.NewGuid(), Now);

        act.Should().Throw<ArgumentException>().WithParameterName("email");
    }

    [Fact]
    public void Create_AcceptsAnAddressOfExactlyTheLongestLength()
    {
        var longest = new string('a', GameNightGuestEmailOptOut.MaxEmailLength - 12) + "@example.com";
        longest.Length.Should().Be(GameNightGuestEmailOptOut.MaxEmailLength);

        var optOut = GameNightGuestEmailOptOut.Create(longest, Guid.NewGuid(), Now);

        optOut.Email.Should().Be(longest);
    }

    [Fact]
    public void Create_RecordsWhenItHappenedAndThroughWhichInvitation()
    {
        var invitationId = Guid.NewGuid();

        var optOut = GameNightGuestEmailOptOut.Create("marco@example.com", invitationId, Now);

        optOut.OptedOutAt.Should().Be(Now);
        optOut.SourceInvitationId.Should().Be(invitationId);
        optOut.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_RefusesAnEmptyInvitationId()
    {
        var act = () => GameNightGuestEmailOptOut.Create("marco@example.com", Guid.Empty, Now);

        act.Should().Throw<ArgumentException>().WithParameterName("sourceInvitationId");
    }

    [Fact]
    public void Normalize_IsTheRuleTheSendsUseToLookTheAddressUp()
    {
        GameNightGuestEmailOptOut.Normalize("  Marco@Example.COM ").Should().Be("marco@example.com");
    }
}
