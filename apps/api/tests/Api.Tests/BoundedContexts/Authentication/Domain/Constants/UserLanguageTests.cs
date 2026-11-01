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
    // Garbage in a header must not throw — and a `q` that is not a number in [0, 1] drops the tag instead of
    // being read as 1 (a malformed weight is not the highest one).
    [InlineData("it;q=abc, en;q=0.5", "en")]
    [InlineData("it;q=abc", "en")]
    [InlineData("it;q=5, en;q=0.5", "en")]
    [InlineData("it;q=-1, en;q=0.5", "en")]
    [InlineData("it;q=NaN, en;q=0.5", "en")]
    [InlineData("it;q=Infinity, en;q=0.5", "en")]
    [InlineData(";;;,,,", "en")]
    // The ways a real `q` is written.
    [InlineData("en;q=0.1, it;q=1.0", "it")]
    [InlineData("en;q=.1, it;q=.5", "it")]
    [InlineData("en;q=1e-1, it;q=0.5", "it")]
    [InlineData("en;q=0.1, it ; q=0.9", "it")]
    // Other parameters beside `q` do not hide it.
    [InlineData("it;q=0.5;x=y, en;q=0.4", "it")]
    [InlineData("it;x=y;q=0.2, en;q=0.4", "en")]
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
    public void Resolve_MoreTagsThanTheBound_AreNotRead_EvenInAShortHeader()
    {
        // Sixteen tags are read; the seventeenth is not. The header is 50 bytes: only the tag bound can stop it.
        var fifteenThenIt = string.Concat(Enumerable.Repeat("de,", 15)) + "it";
        var sixteenThenIt = string.Concat(Enumerable.Repeat("de,", 16)) + "it";

        UserLanguage.Resolve(null, fifteenThenIt).Should().Be("it");
        UserLanguage.Resolve(null, sixteenThenIt).Should().Be("en");
    }

    [Fact]
    public void Resolve_ALongerHeaderThanTheBound_IsNotRead_EvenWithFewTags()
    {
        // Two tags only; the supported one sits past the byte bound. Only the length bound can stop it.
        var header = new string('x', 300) + ",it";

        UserLanguage.Resolve(null, header).Should().Be("en");
    }

    [Fact]
    public void Resolve_TheCutFallsWhereATagEnds_NotInTheMiddleOfOne()
    {
        // `it;q=0.1` is cut by the bound right after `q=`: read as it stands it would be «q empty», and a
        // `q` that cannot be read must not rank above `en;q=0.9`. The header is cut at the last comma
        // before the bound, so the half tag is never seen.
        var prefix = "en;q=0.9," + new string('x', 251 - "en;q=0.9,".Length - 1) + ",";
        prefix.Length.Should().Be(251);
        var header = prefix + "it;q=0.1,fr";

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
