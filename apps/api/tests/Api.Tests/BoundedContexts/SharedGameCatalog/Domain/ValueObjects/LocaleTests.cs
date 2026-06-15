using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Tests for the Locale value object used by shared_game_translations.
/// Issue #2339 — sub-PR 1/3 Wave 1 (Task 1).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class LocaleTests
{
    [Theory]
    [InlineData("it", "it")]
    [InlineData("EN", "en")]
    [InlineData("fr", "fr")]
    [InlineData("en-GB", "en-GB")]
    [InlineData("EN-gb", "en-GB")]
    [InlineData("it-IT", "it-IT")]
    public void Create_ValidIso_NormalizesAndAccepts(string raw, string expected)
    {
        var locale = Locale.Create(raw);
        locale.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("english")]
    [InlineData("xx-yy-zz")]
    [InlineData("12")]
    [InlineData("a")]
    public void Create_InvalidIso_Throws(string raw)
    {
        var act = () => Locale.Create(raw);
        act.Should().Throw<InvalidLocaleException>();
    }

    [Fact]
    public void Create_Null_Throws()
    {
        var act = () => Locale.Create(null!);
        act.Should().Throw<InvalidLocaleException>();
    }

    [Fact]
    public void CanonicalEn_HasValue_en()
    {
        Locale.CanonicalEn.Value.Should().Be("en");
    }

    [Fact]
    public void Equals_SameValue_True()
    {
        var a = Locale.Create("it");
        var b = Locale.Create("IT");
        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        Locale.Create("it").ToString().Should().Be("it");
    }
}
