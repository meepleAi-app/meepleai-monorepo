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

    [Fact]
    public void ExpandTermsToTsQuery_Italian_SetupClusterIsFullySymmetric()
    {
        // Completes the setup/preparazione/allestimento symmetric cluster.
        KeywordSearchService.ExpandTermsToTsQuery("allestimento", "italian")
            .Should().Be("(allestimento | setup | preparazione)");
    }

    [Fact]
    public void ExpandTermsToTsQuery_Italian_SingularPlayerHeadAlsoExpands()
    {
        // Singular "giocatore" head expands identically to the plural entry.
        KeywordSearchService.ExpandTermsToTsQuery("giocatore", "italian")
            .Should().Be("(giocatore | numero <-> giocatori)");
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

    // ---------------------------------------------------------------------
    // Slice A regression (found in code review): BuildTsQuery routing.
    // The production hybrid path always passes a non-empty BoostTerms list
    // (appsettings HybridSearch:BoostTerms), which used to route into a
    // ":A"/":B" weighted branch that BOTH (a) shadowed synonym expansion and
    // (b) matched nothing (the search_vector has no setweight, so all lexemes
    // are weight D and ":A"/":B" query labels match zero rows). These tests
    // pin that the non-phrase path always expands and NEVER emits weight
    // labels, regardless of any BoostTerms configuration.
    // ---------------------------------------------------------------------

    [Fact]
    public void BuildTsQuery_ItalianNonPhrase_ExpandsSynonyms()
    {
        // Head-token case is preserved verbatim ("Setup"); to_tsquery lowercases lexemes at match
        // time, so casing does not affect retrieval.
        KeywordSearchService.BuildTsQuery("Setup per giocatori", phraseSearch: false, "italian")
            .Should().Be("(Setup | preparazione | allestimento) | per | (giocatori | numero <-> giocatori)");
    }

    [Fact]
    public void BuildTsQuery_NonPhrase_NeverEmitsBrokenWeightLabels()
    {
        // ":A"/":B" match nothing on the un-weighted search_vector — they must
        // never appear in a generated tsquery.
        var result = KeywordSearchService.BuildTsQuery("Setup per giocatori", phraseSearch: false, "italian");
        result.Should().NotContain(":A");
        result.Should().NotContain(":B");
    }

    [Fact]
    public void BuildTsQuery_EnglishNonPhrase_PlainOrJoin()
    {
        KeywordSearchService.BuildTsQuery("board setup rules", phraseSearch: false, "english")
            .Should().Be("board | setup | rules");
    }

    [Fact]
    public void BuildTsQuery_PhraseSearch_UsesProximityOperator_NoExpansion()
    {
        // Phrase search (quoted query) stays an exact proximity match, unchanged by Slice A.
        KeywordSearchService.BuildTsQuery("en passant", phraseSearch: true, "english")
            .Should().Be("en <-> passant");
    }

    // --- #3338 WP1c: synonym-aware heading-match term expansion ---

    [Fact]
    public void ExpandHeadingMatchTerms_Italian_AddsSetupSynonyms_SoSetupQueryMatchesPreparazioneHeading()
    {
        var result = KeywordSearchService.ExpandHeadingMatchTerms(new[] { "setup", "giocatori" }, "italian");

        result.Should().Contain("setup");
        result.Should().Contain("preparazione"); // native rulebook lexeme → matches heading "Preparazione"
        result.Should().Contain("allestimento");
    }

    [Fact]
    public void ExpandHeadingMatchTerms_NonTabledConfig_ReturnsTermsUnchanged()
    {
        var terms = new[] { "setup", "scoring" };

        KeywordSearchService.ExpandHeadingMatchTerms(terms, "english")
            .Should().BeEquivalentTo(terms, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExpandHeadingMatchTerms_NullOrBlankConfig_ReturnsTermsUnchanged(string? config)
    {
        // A loose mock of ResolveFtsConfigAsync returns default(string) = null; expansion must be a
        // safe no-op rather than throwing on Dictionary.TryGetValue(null).
        var terms = new[] { "setup" };

        KeywordSearchService.ExpandHeadingMatchTerms(terms, config)
            .Should().BeEquivalentTo(terms);
    }

    [Fact]
    public void ExpandHeadingMatchTerms_DeduplicatesAndKeepsLengthAtLeastThree()
    {
        // "preparazione" already present + its synonym set includes "setup"; no dupes, and any <3-char
        // token is dropped (the ComputeHeadingMatchBoost contract).
        var result = KeywordSearchService.ExpandHeadingMatchTerms(new[] { "setup", "preparazione", "di" }, "italian");

        result.Should().OnlyHaveUniqueItems();
        result.Should().NotContain("di");
        result.Should().Contain(new[] { "setup", "preparazione", "allestimento" });
    }
}
