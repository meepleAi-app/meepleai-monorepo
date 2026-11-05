using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// #4434 (a) — le funzioni pure del banco di qualità dell'estrazione. Le metriche vanno provate da
/// sole, prima di misurare un estrattore: un banco il cui metro è sbagliato non dà un rosso, dà
/// numeri plausibili e sbagliati. Ogni test nomina un caso che il metro deve distinguere.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ExtractionMetricsTests
{
    // ─── Normalize ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("  Gioco   DI\tCarte \n", "gioco di carte")]
    [InlineData("ofﬁcina", "officina")]                      // ligatura ﬁ → fi (NFKC)
    [InlineData("tre­no", "treno")]                          // trattino morbido
    [InlineData("a–b — c", "a-b - c")]                  // trattini tipografici
    [InlineData("l’asso “di” cuori", "l'asso \"di\" cuori")] // apici e virgolette
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_UnifiesTypographyButNotMeaning(string? raw, string expected)
    {
        ExtractionTextMatcher.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    public void Normalize_KeepsDiacritics_BecauseGermanAndItalianWordsDifferByThem()
    {
        // «über» e «uber» sono parole diverse: se la normalizzazione le unisse, un OCR che perde la
        // dieresi risulterebbe perfetto e il banco non vedrebbe il difetto.
        ExtractionTextMatcher.Normalize("Über").Should().NotBe(ExtractionTextMatcher.Normalize("Uber"));
        ExtractionTextMatcher.Normalize("perché").Should().NotBe(ExtractionTextMatcher.Normalize("perche"));
    }

    // ─── ParagraphRecall / TableCellRecall (a livello di testo, valgono per ogni estrattore) ──

    [Fact]
    public void ParagraphRecall_CountsParagraphsFoundContiguously()
    {
        var text = "Intro. Il mercante paga due monete. Fine.";

        ExtractionMetrics.ParagraphRecall(
            ["Il mercante paga due monete.", "Il bandito ruba una carta."], text)
            .Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void ParagraphRecall_IsBlindToLayoutWhitespace()
    {
        ExtractionMetrics.ParagraphRecall(
            ["Il mercante paga due monete."], "Il mercante\npaga   due\tmonete.")
            .Should().Be(1.0);
    }

    [Fact]
    public void ParagraphRecall_IsNull_WhenNothingIsExpected()
    {
        ExtractionMetrics.ParagraphRecall([], "qualunque testo").Should().BeNull();
    }

    [Fact]
    public void TableCellRecall_CountsCellsFoundInTheText()
    {
        ExtractionMetrics.TableCellRecall(["Mercante", "Bandito", "Oste", "Fabbro"], "mercante 2 bandito 3 oste 1")
            .Should().BeApproximately(0.75, 1e-9);
    }

    // ─── HeadingRecall (strutturale: n/d per chi non classifica gli elementi) ─────────────────

    [Fact]
    public void HeadingRecall_IsNull_WhenTheExtractorDoesNotClassifyElements()
    {
        // Docnet restituisce testo, non elementi: «0 titoli trovati» sarebbe un falso zero.
        ExtractionMetrics.HeadingRecall(["Setup"], observedHeadings: null).Should().BeNull();
    }

    [Fact]
    public void HeadingRecall_IsZero_WhenTheExtractorClassifiesButFindsNone()
    {
        // Qui invece lo zero è vero: l'estrattore sa distinguere i titoli e non ne ha trovato nessuno.
        ExtractionMetrics.HeadingRecall(["Setup", "Turni"], observedHeadings: []).Should().Be(0.0);
    }

    [Fact]
    public void HeadingRecall_MatchesAnObservedHeadingEqualOrContainingTheExpected()
    {
        // Unstructured può accorpare un numero di sezione o un'andata a capo nel titolo.
        ExtractionMetrics.HeadingRecall(
            ["Preparazione", "Turno di gioco", "Fine partita"],
            ["2. PREPARAZIONE", "Turno di gioco"])
            .Should().BeApproximately(2.0 / 3.0, 1e-9);
    }

    // ─── TableCellF1 (strutturale: n/d finché nessun estrattore espone le celle) ──────────────

    [Fact]
    public void TableCellF1_IsNull_WhenTheExtractorExposesNoCells()
    {
        ExtractionMetrics.TableCellF1(["a", "b"], observedCells: null).Should().BeNull();
    }

    [Fact]
    public void TableCellF1_IsOne_ForAnExactMatchRegardlessOfOrder()
    {
        ExtractionMetrics.TableCellF1(["a", "b", "c"], ["C", "a", " b "]).Should().Be(1.0);
    }

    [Fact]
    public void TableCellF1_PenalizesBothMissingAndSpuriousCells()
    {
        // attese {a,b,c,d}; osservate {a,b,x}: TP=2, precisione 2/3, richiamo 2/4 → F1 = 4/7.
        ExtractionMetrics.TableCellF1(["a", "b", "c", "d"], ["a", "b", "x"])
            .Should().BeApproximately(4.0 / 7.0, 1e-9);
    }

    [Fact]
    public void TableCellF1_IsZero_WhenCellsAreSupportedButNoneWasFound()
    {
        ExtractionMetrics.TableCellF1(["a", "b"], observedCells: []).Should().Be(0.0);
    }

    [Fact]
    public void TableCellF1_CountsDuplicatesAsMultiset()
    {
        // due celle «1» attese, una sola osservata: la seconda non può essere coperta dalla prima.
        ExtractionMetrics.TableCellF1(["1", "1"], ["1"]).Should().BeApproximately(2.0 / 3.0, 1e-9);
    }

    // ─── ReadingOrder (le due colonne) ─────────────────────────────────────────────────────────

    [Fact]
    public void ReadingOrder_IsOne_WhenMarkersAppearInTheExpectedSequence()
    {
        ExtractionMetrics.ReadingOrder(["alfa", "beta", "gamma"], "xx alfa yy beta zz gamma").Should().Be(1.0);
    }

    [Fact]
    public void ReadingOrder_DropsWhenColumnsAreInterleaved()
    {
        // attesa: alfa(col 1) beta(col 1) gamma(col 2) delta(col 2); osservata: righe alternate.
        // Coppie (alfa,beta) ok, (beta,gamma) ko, (gamma,delta) ok → 2/3.
        ExtractionMetrics.ReadingOrder(["alfa", "beta", "gamma", "delta"], "alfa gamma beta delta")
            .Should().BeApproximately(2.0 / 3.0, 1e-9);
    }

    [Fact]
    public void ReadingOrder_CountsAMissingMarkerAsBrokenPairs()
    {
        ExtractionMetrics.ReadingOrder(["alfa", "beta", "gamma"], "alfa gamma").Should().Be(0.0);
    }

    [Fact]
    public void ReadingOrder_IsNull_WithFewerThanTwoMarkers()
    {
        ExtractionMetrics.ReadingOrder(["solo"], "solo").Should().BeNull();
    }

    // ─── ScanWordRecall (testo trovato su una pagina scansionata) ─────────────────────────────

    [Fact]
    public void ScanWordRecall_IsZero_WhenThePageYieldsNoText()
    {
        // È il risultato ATTESO di Docnet su una scansione (#4437): un numero vero, non un errore.
        ExtractionMetrics.ScanWordRecall(["mercato", "carta"], string.Empty, ocrEditTolerance: 0).Should().Be(0.0);
    }

    [Fact]
    public void ScanWordRecall_MatchesWholeWordsOnly()
    {
        // «carte» non copre «carta», e «mercato» non è contenuto in «mercatore».
        ExtractionMetrics.ScanWordRecall(["mercato", "carta"], "il mercatore con le carte", ocrEditTolerance: 0)
            .Should().Be(0.0);
    }

    [Fact]
    public void ScanWordRecall_ToleratesOcrEditsOnlyWhenAsked()
    {
        const string ocr = "mercat0 carta"; // lo zero al posto della «o»: un errore tipico dell'OCR

        ExtractionMetrics.ScanWordRecall(["mercato", "carta"], ocr, ocrEditTolerance: 0)
            .Should().BeApproximately(0.5, 1e-9);
        ExtractionMetrics.ScanWordRecall(["mercato", "carta"], ocr, ocrEditTolerance: 0.2)
            .Should().Be(1.0);
    }
}
