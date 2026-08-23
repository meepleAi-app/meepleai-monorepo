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

    // ---------------------------------------------------------------------
    // #3768: il nome del gioco non discrimina DENTRO il suo stesso gioco.
    //
    // La ricerca lessicale gira gia' filtrata per GameId, quindi ogni chunk
    // candidato appartiene a quel gioco e il nome vi compare ovunque: e' un
    // termine a IDF nullo. Pesarlo premia i chunk che lo ripetono di piu' —
    // colophon, copertine, intestazioni — cioe' il testo senza regole.
    //
    // Misurato su staging per `catan-setup-it`: il rango 1 del braccio
    // lessicale era la pagina di copyright ("Copyright © 2025 CATAN GmbH...",
    // ts_rank_cd 0.2256), davanti al miglior chunk di regole. Quei tre
    // candidati sono cio' che il gioco manda alla fusione globale, quindi il
    // contenuto pertinente non arrivava nemmeno a essere valutato.
    // ---------------------------------------------------------------------

    [Fact]
    public void BuildTsQuery_WithGameTitle_DropsTheGameNameToken()
    {
        var result = KeywordSearchService.BuildTsQuery(
            "Come si prepara il tabellone in Catan", phraseSearch: false, "english", gameTitle: "Catan");

        result.Should().NotContainEquivalentOf("catan");
        result.Should().Contain("tabellone", "gli altri token restano: il filtro toglie il nome, non la domanda");
    }

    [Fact]
    public void BuildTsQuery_WithMultiWordGameTitle_DropsEveryTokenOfTheTitle()
    {
        // "Terraforming Mars": entrambi i token vanno tolti, altrimenti "mars" continua a premiare
        // le pagine che ripetono il titolo.
        var result = KeywordSearchService.BuildTsQuery(
            "Come si prepara Terraforming Mars per due giocatori", phraseSearch: false, "english",
            gameTitle: "Terraforming Mars");

        result.Should().NotContainEquivalentOf("terraforming");
        result.Should().NotContainEquivalentOf("mars");
        result.Should().Contain("giocatori");
    }

    [Fact]
    public void BuildTsQuery_WithoutGameTitle_IsUnchanged()
    {
        // Il percorso che non conosce il gioco (ricerca non filtrata per GameId) deve restare
        // identico: li' il nome del gioco E' il segnale che sceglie il gioco.
        var withTitle = KeywordSearchService.BuildTsQuery("setup per giocatori", phraseSearch: false, "italian");
        var explicitNull = KeywordSearchService.BuildTsQuery("setup per giocatori", phraseSearch: false, "italian", gameTitle: null);

        explicitNull.Should().Be(withTitle);
    }

    [Fact]
    public void BuildTsQuery_QueryMadeOnlyOfTheGameName_ReturnsEmpty()
    {
        // Caso limite reale: "Catan" su Catan. Senza token residui non c'e' nulla da cercare, e una
        // tsquery vuota va riconosciuta dal chiamante invece di finire in to_tsquery() come stringa
        // vuota — che e' un errore SQL, non un risultato vuoto.
        KeywordSearchService.BuildTsQuery("Catan", phraseSearch: false, "english", gameTitle: "Catan")
            .Should().BeEmpty();
    }

    [Fact]
    public void BuildTsQuery_GameTitleMatchIsCaseInsensitive()
    {
        KeywordSearchService.BuildTsQuery("come si vince a CATAN", phraseSearch: false, "english", gameTitle: "Catan")
            .Should().NotContainEquivalentOf("catan");
    }

    [Fact]
    public void BuildTsQuery_GameNameFollowedByPunctuation_IsStillDropped()
    {
        // Il caso REALE, non quello comodo: ogni query canonica finisce con "?", quindi il token
        // arriva come "Catan?" — SanitizeQuery toglie gli operatori tsquery ma NON la punteggiatura.
        // Confrontando i token grezzi il filtro falliva proprio sulle query di produzione, ed è
        // sfuggito ai primi test perché li avevo scritti senza punto interrogativo.
        var result = KeywordSearchService.BuildTsQuery(
            "Come si prepara il tabellone e si piazzano i due insediamenti e le strade iniziali in Catan?",
            phraseSearch: false, "english", gameTitle: "Catan");

        result.Should().NotContainEquivalentOf("catan");
        result.Should().Contain("insediamenti");
    }

    [Fact]
    public void BuildTsQuery_GameTitleWithPunctuation_MatchesTheQueryToken()
    {
        // Simmetrico: la punteggiatura può stare nel TITOLO ("Catan: Cities & Knights").
        var result = KeywordSearchService.BuildTsQuery(
            "come funzionano le citta in Catan", phraseSearch: false, "english",
            gameTitle: "Catan: Cities");

        result.Should().NotContainEquivalentOf("catan");
    }

    [Fact]
    public void BuildTsQuery_PhraseSearch_KeepsTheGameNameOut()
    {
        // Anche il ramo phrase deve filtrare: un <-> che include il nome del gioco cerca una
        // sequenza che nel manuale compare solo in copertina.
        KeywordSearchService.BuildTsQuery("preparazione in Catan", phraseSearch: true, "english", gameTitle: "Catan")
            .Should().NotContainEquivalentOf("catan");
    }
}
