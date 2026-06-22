using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Unit tests for <see cref="SharedTranslationValidationRules"/>. Issue #2379 (F1).
/// Consolidates the <c>BeValidLocale</c> helper previously duplicated in three
/// translation command validators.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedTranslationValidationRulesTests
{
    [Theory]
    [InlineData("it")]
    [InlineData("en")]
    [InlineData("en-GB")]
    [InlineData("EN-gb")] // case-insensitive, normalised by Locale.Create
    public void BeValidLocale_AcceptsIso639(string raw)
    {
        SharedTranslationValidationRules.BeValidLocale(raw).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("english")]
    [InlineData("italiano")]
    [InlineData("1234")]
    [InlineData("e")]
    [InlineData("en-G")]
    [InlineData("en-GBR")]
    public void BeValidLocale_RejectsMalformed(string raw)
    {
        SharedTranslationValidationRules.BeValidLocale(raw).Should().BeFalse();
    }

    [Fact]
    public void BeValidLocale_NullSafe()
    {
        SharedTranslationValidationRules.BeValidLocale(null!).Should().BeFalse();
    }
}
