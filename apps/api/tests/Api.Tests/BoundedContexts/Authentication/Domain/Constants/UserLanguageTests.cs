using Api.BoundedContexts.Authentication.Domain.Constants;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Constants;

/// <summary>
/// #4783: the language a new account starts with. Every account used to be born <c>en</c>, so an Italian
/// speaker who never opened Settings → Preferences sent their friends game night invitations in English.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class UserLanguageTests
{
    [Theory]
    [InlineData("it", "it")]
    [InlineData("IT", "it")]
    [InlineData("it-IT", "it")]
    [InlineData("it_CH", "it")]
    [InlineData(" it ", "it")]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]
    public void Resolve_AnExplicitSupportedLocale_IsUsed(string locale, string expected) =>
        UserLanguage.Resolve(locale).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_NothingAtAll_IsTheDefault(string? nothing) =>
        UserLanguage.Resolve(nothing, nothing).Should().Be(UserLanguage.Default).And.Be("en");

    [Theory]
    // The browser of an Italian speaker, as Chrome and Firefox send it.
    [InlineData("it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7", "it")]
    [InlineData("it", "it")]
    [InlineData("en-US,en;q=0.9", "en")]
    // Ranked by quality, not by position.
    [InlineData("en;q=0.5,it;q=0.9", "it")]
    // A tie is the order of appearance.
    [InlineData("en,it", "en")]
    [InlineData("it,en", "it")]
    // An unsupported tag is skipped, not taken for the default: the next supported one wins.
    [InlineData("de-DE,de;q=0.9,it;q=0.5", "it")]
    [InlineData("fr-FR,fr;q=0.9,en;q=0.8", "en")]
    // «not acceptable» and the wildcard never match.
    [InlineData("it;q=0,en", "en")]
    [InlineData("*,it;q=0.1", "it")]
    // Nothing supported at all → the default, not an error.
    [InlineData("de,fr;q=0.9", "en")]
    [InlineData("*", "en")]
    // Garbage in a header must not throw.
    [InlineData("it;q=abc", "it")]
    [InlineData(";;;,,,", "en")]
    public void Resolve_TheAcceptLanguageHeader_PicksTheBestSupportedLanguage(string header, string expected) =>
        UserLanguage.Resolve(null, header).Should().Be(expected);

    [Fact]
    public void Resolve_WhatTheClientSaysTheUserSees_BeatsTheHeader()
    {
        // A German browser shown the Italian interface (the product's default locale): the interface the user
        // is looking at is the better evidence of what they read.
        UserLanguage.Resolve("it", "de-DE,de;q=0.9").Should().Be("it");
        UserLanguage.Resolve("en", "it-IT,it;q=0.9").Should().Be("en");
    }

    [Fact]
    public void Resolve_AnUnsupportedExplicitLocale_FallsThroughToTheHeader()
    {
        UserLanguage.Resolve("fr", "it-IT,it;q=0.9").Should().Be("it");
        UserLanguage.Resolve("fr", "de").Should().Be("en");
    }

    [Fact]
    public void Resolve_AHeaderFarLongerThanAnyRealOne_IsBoundedNotRejected()
    {
        // The supported tag sits past the bound, so it is not read: the result is the default, and the call
        // returns instead of parsing an unbounded string.
        var header = string.Concat(Enumerable.Repeat("xx;q=0.1,", 100)) + "it";

        UserLanguage.Resolve(null, header).Should().Be("en");
    }

    [Fact]
    public void Resolve_OnlyLanguagesTheProductCanWriteIn_AreEverChosen()
    {
        // The selector in Preferences offers French, and Preferences may store it; a NEW account does not
        // start in a language nothing is translated into.
        UserLanguage.Resolve("fr").Should().Be("en");
        UserLanguage.Resolve(null, "fr-FR,fr;q=0.9").Should().Be("en");
    }
}
