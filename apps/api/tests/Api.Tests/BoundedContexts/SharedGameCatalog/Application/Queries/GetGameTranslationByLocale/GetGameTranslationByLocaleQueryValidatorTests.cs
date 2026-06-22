using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslationByLocale;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslationByLocale;

/// <summary>
/// Unit tests for <see cref="GetGameTranslationByLocaleQueryValidator"/>.
/// Issue #2379 (F5) — surfaces "Invalid ISO 639-1 locale" via FluentValidation
/// (HTTP 422) instead of degrading to the middleware-mapped 400 from the
/// handler's <c>Locale.Create</c> throw.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetGameTranslationByLocaleQueryValidatorTests
{
    private readonly GetGameTranslationByLocaleQueryValidator _sut = new();

    [Fact]
    public void Valid_NoErrors()
    {
        var q = new GetGameTranslationByLocaleQuery(Guid.NewGuid(), "it");

        var result = _sut.TestValidate(q);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyGameId_FailsGameIdRule()
    {
        var q = new GetGameTranslationByLocaleQuery(Guid.Empty, "it");

        var result = _sut.TestValidate(q);

        result.ShouldHaveValidationErrorFor(x => x.GameId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("english")]
    [InlineData("1234")]
    [InlineData("en-GBR")]
    public void InvalidLocale_FailsLocaleRule(string locale)
    {
        var q = new GetGameTranslationByLocaleQuery(Guid.NewGuid(), locale);

        var result = _sut.TestValidate(q);

        result.ShouldHaveValidationErrorFor(x => x.Locale);
    }

    [Fact]
    public void InvalidLocale_PreservesIsoMessage()
    {
        var q = new GetGameTranslationByLocaleQuery(Guid.NewGuid(), "english");

        var result = _sut.TestValidate(q);

        var localeErrors = result.ShouldHaveValidationErrorFor(x => x.Locale);
        localeErrors.Should().Contain(e =>
            e.ErrorMessage.Contains("ISO 639-1", StringComparison.OrdinalIgnoreCase));
    }
}
