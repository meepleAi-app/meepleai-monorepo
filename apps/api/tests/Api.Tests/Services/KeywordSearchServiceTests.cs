using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// #2569 follow-up (RAG answer-quality): keyword FTS now honours per-game language.
/// <see cref="KeywordSearchService.ResolveFtsConfig"/> maps a language code to a PostgreSQL FTS
/// config. English keeps using the indexed 'english' search_vector column; non-english is matched
/// against a query-time to_tsvector with the SAME config, so the query/vector configs always agree
/// (sidestepping the #2569 footgun without a multilingual column). Unknown -> 'simple'.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KeywordSearchServiceTests
{
    [Theory]
    [InlineData("en", "english")]
    [InlineData("eng", "english")]
    [InlineData("English", "english")]
    [InlineData("it", "italian")]
    [InlineData("ita", "italian")]
    [InlineData("italian", "italian")]
    [InlineData("italiano", "italian")]
    [InlineData("de", "german")]
    [InlineData("fr", "french")]
    [InlineData("es", "spanish")]
    [InlineData("pt", "portuguese")]
    [InlineData("nl", "dutch")]
    [InlineData("", "english")]   // empty/unspecified -> default english
    [InlineData("xx", "simple")]  // unknown language -> simple (safe under query-time to_tsvector)
    public void ResolveFtsConfig_MapsLanguageToPostgresConfig(string language, string expected)
    {
        KeywordSearchService.ResolveFtsConfig(language).Should().Be(expected);
    }

    // ---------------------------------------------------------------------
    // Slice A (RAG answer-quality): keyword-arm intent synonym expansion.
    // ExpandTermsToTsQuery expands Italian intent synonyms (e.g. setup ->
    // preparazione/allestimento) into grouped OR-alternations on the KEYWORD
    // arm only, so the failing "Setup per N giocatori" query also matches the
    // Italian rulebook lexemes. Non-tabled configs (english/simple/...) must
    // reproduce the pre-existing OR join verbatim (no recall/precision drift).
    // ---------------------------------------------------------------------

    [Fact]
    public void ExpandTermsToTsQuery_Italian_WrapsTokenWithSynonymsInOrGroup()
    {
        KeywordSearchService.ExpandTermsToTsQuery("setup", "italian")
            .Should().Be("(setup | preparazione | allestimento)");
    }

    [Fact]
    public void ExpandTermsToTsQuery_Italian_MultiWordSynonymUsesProximityOperator()
    {
        // "numero giocatori" is a multi-word synonym -> must join with <-> (a bare
        // space is a to_tsquery syntax error).
        KeywordSearchService.ExpandTermsToTsQuery("giocatori", "italian")
            .Should().Be("(giocatori | numero <-> giocatori)");
    }

    [Fact]
    public void ExpandTermsToTsQuery_Italian_ExpandsEachTokenIndependently()
    {
        KeywordSearchService.ExpandTermsToTsQuery("setup giocatori", "italian")
            .Should().Be("(setup | preparazione | allestimento) | (giocatori | numero <-> giocatori)");
    }

    [Fact]
    public void ExpandTermsToTsQuery_Italian_LeavesUnknownTokensBare()
    {
        // Tokens without a synonym entry stay bare (no empty groups, no noise).
        KeywordSearchService.ExpandTermsToTsQuery("piazza operai", "italian")
            .Should().Be("piazza | operai");
    }

    [Fact]
    public void ExpandTermsToTsQuery_Italian_LookupIsCaseInsensitive_HeadTokenPreserved()
    {
        KeywordSearchService.ExpandTermsToTsQuery("Setup", "italian")
            .Should().Be("(Setup | preparazione | allestimento)");
    }

    [Fact]
    public void ExpandTermsToTsQuery_Italian_SynonymDirectionIsSymmetric()
    {
        // An Italian query "preparazione" should also match the English loanword
        // section title "Setup" used in many Italian rulebooks.
        KeywordSearchService.ExpandTermsToTsQuery("preparazione", "italian")
            .Should().Be("(preparazione | setup | allestimento)");
    }

    [Theory]
    [InlineData("english")]
    [InlineData("simple")]
    [InlineData("german")]
    public void ExpandTermsToTsQuery_NonTabledConfig_ReproducesPlainOrJoin(string ftsConfig)
    {
        // Non-Italian configs get NO expansion — identical to the pre-slice
        // `sanitizedQuery.Replace(" ", " | ")` behaviour (zero regression risk).
        KeywordSearchService.ExpandTermsToTsQuery("setup per giocatori", ftsConfig)
            .Should().Be("setup | per | giocatori");
    }

    [Fact]
    public void ExpandTermsToTsQuery_SingleToken_NonTabled_ReturnsBareToken()
    {
        KeywordSearchService.ExpandTermsToTsQuery("castling", "english")
            .Should().Be("castling");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ExpandTermsToTsQuery_EmptyOrWhitespace_ReturnsEmpty(string input)
    {
        KeywordSearchService.ExpandTermsToTsQuery(input, "italian")
            .Should().BeEmpty();
    }
}
