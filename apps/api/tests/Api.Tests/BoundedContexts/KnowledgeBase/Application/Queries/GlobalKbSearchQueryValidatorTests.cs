using Api.BoundedContexts.KnowledgeBase.Application.Queries.GlobalKbSearch;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for <see cref="GlobalKbSearchQueryValidator"/> — facet validation.
/// Issue #1686 (Task 1+2): DocType (list) and Language (single string) allowlist enforcement.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GlobalKbSearchQueryValidatorTests
{
    private static readonly Guid AnyUserId = Guid.NewGuid();

    private static GlobalKbSearchQuery BuildQuery(
        IReadOnlyList<string>? docType = null,
        Guid? gameId = null,
        string? language = null,
        string query = "test query",
        int limit = 10) =>
        new(
            Query: query,
            Limit: limit,
            Cursor: null,
            Mode: SearchMode.Hybrid,
            MinScore: 0.0,
            UserId: AnyUserId,
            Role: UserRole.User,
            DocType: docType,
            GameId: gameId,
            Language: language);

    // ─── DocType: null and empty list treated as "no filter" ────────────────

    [Fact]
    public async Task NullDocType_Passes()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(BuildQuery(docType: null));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyDocTypeList_Passes()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(BuildQuery(docType: Array.Empty<string>()));
        result.IsValid.Should().BeTrue();
    }

    // ─── DocType: allowlist values pass ──────────────────────────────────────

    [Theory]
    [InlineData("base")]
    [InlineData("expansion")]
    [InlineData("errata")]
    [InlineData("homerule")]
    public async Task DocTypeAllowlistValue_Passes(string allowedValue)
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(BuildQuery(docType: new[] { allowedValue }));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DocTypeWithMixedCase_Passes()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(
            BuildQuery(docType: new[] { "Base", "EXPANSION", "Errata" }));
        result.IsValid.Should().BeTrue();
    }

    // ─── DocType: unknown values fail with actionable message ────────────────

    [Fact]
    public async Task DocTypeWithUnknownValue_FailsValidation()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(
            BuildQuery(docType: new[] { "unknown-type" }));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("DocType", StringComparison.Ordinal));
        // D-12: error message enumerates allowed values
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("base", StringComparison.OrdinalIgnoreCase) &&
            e.ErrorMessage.Contains("expansion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DocTypeWithEmptyStringElement_FailsValidation()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(
            BuildQuery(docType: new[] { "base", string.Empty }));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task DocTypeWithWhitespaceElement_FailsValidation()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(
            BuildQuery(docType: new[] { "base", "   " }));
        result.IsValid.Should().BeFalse();
    }

    // ─── DocType: hard cap on list length (D-7) ──────────────────────────────

    [Fact]
    public async Task DocTypeListAboveCap_FailsValidation()
    {
        var validator = new GlobalKbSearchQueryValidator();
        // Cap = 10; pass 11 identical valid values to exceed length while keeping each valid
        var oversized = Enumerable.Repeat("base", 11).ToArray();

        var result = await validator.ValidateAsync(BuildQuery(docType: oversized));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("DocType", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DocTypeListAtCap_Passes()
    {
        var validator = new GlobalKbSearchQueryValidator();
        // Cap = 10; pass exactly 10 entries (allowlist has 4, repetitions are accepted by allowlist)
        var atCap = Enumerable.Repeat("base", 10).ToArray();

        var result = await validator.ValidateAsync(BuildQuery(docType: atCap));

        result.IsValid.Should().BeTrue();
    }

    // ─── Combined: existing rules still apply alongside facets ───────────────

    [Fact]
    public async Task EmptyQueryWithValidDocType_StillFailsOnQuery()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(
            BuildQuery(query: string.Empty, docType: new[] { "base" }));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Query");
    }

    // ─── Language: null passes ───────────────────────────────────────────────

    [Fact]
    public async Task NullLanguage_Passes()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(BuildQuery(language: null));
        result.IsValid.Should().BeTrue();
    }

    // ─── Language: allowlist values pass ─────────────────────────────────────

    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("es")]
    public async Task LanguageAllowlistValue_Passes(string allowedCode)
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(BuildQuery(language: allowedCode));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task LanguageMixedCase_Passes()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(BuildQuery(language: "IT"));
        result.IsValid.Should().BeTrue();
    }

    // ─── Language: unknown / empty / whitespace fail ─────────────────────────

    [Fact]
    public async Task LanguageUnknown_FailsValidation()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(BuildQuery(language: "xx"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "Language");
        // D-12: error message enumerates allowed values
        result.Errors.Should().Contain(e =>
            e.ErrorMessage.Contains("en", StringComparison.OrdinalIgnoreCase) &&
            e.ErrorMessage.Contains("it", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LanguageEmptyString_FailsValidation()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(BuildQuery(language: string.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Language");
    }

    [Fact]
    public async Task LanguageWhitespace_FailsValidation()
    {
        var validator = new GlobalKbSearchQueryValidator();
        var result = await validator.ValidateAsync(BuildQuery(language: "   "));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Language");
    }
}
