using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Il nome del gioco non deve accendere il boost sulla heading, dentro il suo stesso gioco (#3768).
/// </summary>
/// <remarks>
/// <para>
/// <b>Perché.</b> È lo stesso difetto che #3769 ha corretto nel braccio lessicale, lasciato in
/// piedi qui: la ricerca gira già filtrata per <c>GameId</c>, quindi il nome del gioco compare
/// ovunque ed è un termine a IDF nullo. Le heading che lo contengono sono, in un regolamento,
/// proprio quelle senza contenuto — il colophon (<c>catan.com</c>), il piè di pagina
/// (<c>4 of 5 CATAN CN3081</c>), la copertina.
/// </para>
/// <para>
/// <b>Quanto pesa.</b> <see cref="FusionSignals.HeadingMatchBoost"/> è additivo e vale <c>0.15</c>,
/// mentre <c>rrfSum</c> satura a <c>1/61 = 0.0164</c>: un match di heading non inclina il ranking,
/// lo sostituisce. Misurato su staging per <c>catan-setup-it</c>, i tre candidati che Catan mandava
/// alla fusione globale avevano tutti <c>catan</c> nella heading, mentre il chunk con le regole di
/// setup — rango 1 del braccio vettoriale dentro Catan, cosine 0.80544 — restava fuori.
/// </para>
/// <para>
/// <b>Misura del rimedio.</b> Sul dump per-gioco di staging, con il banco offline
/// (<c>rag-fusion-bench.py --per-game</c>): 7/8 → 8/8 sulle query con campione sano, nessuna
/// regressione. Le altre 3 query erano su un campione degradato (#3786) e non sono valutabili.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class HeadingBoostGameNameTests
{
    private const string ItalianQuery =
        "Come si prepara il tabellone e si piazzano i due insediamenti e le strade iniziali in Catan?";

    private static IReadOnlyList<string> Terms(string query, string? gameTitle) =>
        KeywordSearchService.ExpandHeadingMatchTerms(
            FusionSignals.ExtractHeadingMatchTerms(query), ftsConfig: "english", gameTitle: gameTitle);

    [Fact]
    public void TheGameNameIsDroppedFromTheHeadingTerms()
    {
        var terms = Terms(ItalianQuery, "Catan");

        terms.Should().NotContain("catan");
        terms.Should().Contain("tabellone", "gli altri termini della query restano intatti");
    }

    [Fact]
    public void WithoutTheGameNameTheColophonHeadingStopsBeingBoosted()
    {
        // Il caso reale: heading `catan.com`, 12 caratteri di contenuto.
        FusionSignals.ComputeHeadingMatchBoost(Terms(ItalianQuery, gameTitle: null), "catan.com")
            .Should().Be(FusionSignals.HeadingMatchBoost, "è il comportamento che il fix rimuove");

        FusionSignals.ComputeHeadingMatchBoost(Terms(ItalianQuery, "Catan"), "catan.com")
            .Should().Be(0f);
    }

    [Fact]
    public void ThePageFooterStopsBeingBoostedToo()
    {
        // `4 of 5 CATAN CN3081` — l'altra heading che portava un candidato inutile in cima.
        FusionSignals.ComputeHeadingMatchBoost(Terms(ItalianQuery, "Catan"), "4 of 5 CATAN CN3081")
            .Should().Be(0f);
    }

    [Fact]
    public void AHeadingThatMatchesOnRealContentIsStillBoosted()
    {
        // Il fix deve togliere SOLO il nome del gioco: se lo togliesse tutto, spegnerebbe #3270.
        FusionSignals.ComputeHeadingMatchBoost(Terms(ItalianQuery, "Catan"), "Preparazione del tabellone")
            .Should().Be(FusionSignals.HeadingMatchBoost);
    }

    [Fact]
    public void WithoutAGameTitleNothingIsDropped()
    {
        // Percorso senza gameId: lì il nome del gioco è il segnale che SCEGLIE il gioco (#3735).
        // Stessa eccezione dichiarata da #3769 per la tsquery.
        Terms(ItalianQuery, gameTitle: null).Should().Contain("catan");
    }

    [Theory]
    [InlineData("catan")]
    [InlineData("CATAN")]
    [InlineData("  Catan  ")]
    public void TheTitleMatchIgnoresCaseAndSurroundingSpace(string title)
    {
        Terms(ItalianQuery, title).Should().NotContain("catan");
    }

    [Fact]
    public void AMultiWordTitleDropsEachOfItsTokens()
    {
        var terms = Terms("Come funziona il punteggio di fine round in Terraforming Mars?", "Terraforming Mars");

        terms.Should().NotContain("terraforming");
        terms.Should().NotContain("mars");
        terms.Should().Contain("punteggio");
    }

    [Fact]
    public void ATitleTokenShorterThanThreeCharsCannotWidenTheFilter()
    {
        // I termini di heading hanno lunghezza >= 3 per contratto: un titolo con token corti
        // ("7 Wonders") non deve poter cancellare nulla di piu' di quanto nomina.
        var terms = Terms("Come si risolve il conflitto militare in 7 Wonders?", "7 Wonders");

        terms.Should().NotContain("wonders");
        terms.Should().Contain("conflitto");
        terms.Should().Contain("militare");
    }

    [Fact]
    public void AQueryMadeOnlyOfTheGameNameLeavesNoTerms()
    {
        // Nessun termine significa nessun boost, che e' corretto: non c'e' niente su cui premiare
        // una heading. Il chiamante non deve andare in errore, a differenza della tsquery vuota.
        Terms("Catan?", "Catan").Should().BeEmpty();
    }
}
