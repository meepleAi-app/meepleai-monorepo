using Api.SharedKernel.Domain.Validation;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Domain.Validation;

/// <summary>
/// #4780 — the name other players see for a user is never an address. With the account's address at hand, a name
/// that contains it goes; a name with the shape of an address goes in any case, the account's or not — the old
/// address a user without a name kept as one after changing it is not the account's any more.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedKernel")]
[Trait("Issue", "4780")]
public sealed class PlayerDisplayNamesTests
{
    private const string Fallback = "Giocatore";

    [Theory]
    [InlineData(null, "marta@example.com", Fallback)]
    [InlineData("   ", "marta@example.com", Fallback)]
    [InlineData("marta@example.com", "marta@example.com", Fallback)]
    [InlineData("  Marta@Example.COM ", "marta@example.com", Fallback)]
    [InlineData("Marta (marta@example.com)", "marta@example.com", Fallback)]
    [InlineData("user@localhost", "user@localhost", Fallback)]
    [InlineData("vecchio@example.com", "nuovo@example.com", Fallback)]
    [InlineData("DJ@Home.TV", "marta@example.com", Fallback)]
    [InlineData("marta@work", "marta@example.com", "marta@work")]
    [InlineData("  Marta Rossi  ", "marta@example.com", "Marta Rossi")]
    public void ForOthers_WithTheAccountsAddress_DropsItAndAnyOtherAddress(string? name, string email, string shown)
    {
        PlayerDisplayNames.ForOthers(name, email, Fallback).Should().Be(shown);
    }

    [Theory]
    [InlineData("marta@example.com", Fallback)]
    [InlineData("DJ@Home.TV", Fallback)]
    [InlineData("Marta Rossi", "Marta Rossi")]
    [InlineData("marta@work", "marta@work")]
    public void ForOthers_WithoutAnAddress_JudgesByTheShape(string name, string shown)
    {
        PlayerDisplayNames.ForOthers(name, email: null, Fallback).Should().Be(shown);
    }

    [Fact]
    public void ForTheTable_CutsToTheLimitOfASeat()
    {
        PlayerDisplayNames.ForTheTable(new string('m', 128), "marta@example.com", Fallback).Should().HaveLength(50);
    }
}
