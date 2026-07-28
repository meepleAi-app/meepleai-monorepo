using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Chunking;

/// <summary>
/// Epic #3338 WP1a unit tests for <see cref="EmbeddedTitleSplitter"/>: recovering section titles that
/// unstructured's 'fast' strategy glued into body elements (the Terraforming Mars "PREPARAZIONE"
/// case), while NOT false-splitting well-extracted English prose.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class EmbeddedTitleSplitterTests
{
    private static ExtractedElement Body(string text, int page = 1) => new(text, page, "NarrativeText");
    private static ExtractedElement Title(string text, int page = 1) => new(text, page, "Title");

    [Fact]
    public void Split_TmCase_PromotesEmbeddedPreparazioneToSyntheticTitle()
    {
        // The exact shape observed on staging: the setup title is glued between a page-number artifact
        // ("6") and lowercase running prose, inside a body element whose neighbouring heading is "CARTE".
        var el = Body("7 4 8 Se non sei sicuro del funzionamento di una carta, leggi il testo tra "
            + "parentesi. 6 PREPARAZIONE Di seguito viene descritta la preparazione per il gioco da 2 a 5 giocatori.");

        var result = EmbeddedTitleSplitter.Split(new[] { el });

        result.Should().HaveCount(3);
        result[0].ElementType.Should().Be("NarrativeText");
        result[0].Text.Should().EndWith("6");   // head keeps the page-number artifact (goes to prior section)
        result[1].ElementType.Should().Be("Title");
        result[1].Text.Should().Be("PREPARAZIONE");
        result[2].ElementType.Should().Be("NarrativeText");
        result[2].Text.Should().StartWith("Di seguito viene descritta");
        result.Should().OnlyContain(e => e.PageNumber == 1);
    }

    [Fact]
    public void Split_TitleAtStartOfElement_OmitsEmptyHead()
    {
        var el = Body("PREPARAZIONE Di seguito viene descritta la preparazione del gioco.");

        var result = EmbeddedTitleSplitter.Split(new[] { el });

        result.Should().HaveCount(2);
        result[0].ElementType.Should().Be("Title");
        result[0].Text.Should().Be("PREPARAZIONE");
        result[1].Text.Should().StartWith("Di seguito");
    }

    [Fact]
    public void Split_ExistingTitleElement_PassesThroughUnchanged()
    {
        var el = Title("PREPARAZIONE");

        var result = EmbeddedTitleSplitter.Split(new[] { el });

        result.Should().ContainSingle();
        result[0].Should().Be(el);
    }

    [Fact]
    public void Split_PreservesOrderAndSplitsOnlyTheMatchingElement()
    {
        var a = Title("CONTESTO");
        var b = Body("Testo del contesto senza titoli incorporati, tutto minuscolo qui.");
        var c = Body("Riepilogo finale. 6 PREPARAZIONE Di seguito trovi i passi di preparazione.");

        var result = EmbeddedTitleSplitter.Split(new[] { a, b, c });

        result.Should().HaveCount(5); // a, b, [head c, Title, tail c]
        result[0].Should().Be(a);
        result[1].Should().Be(b);
        result[2].Text.Should().EndWith("6");
        result[3].ElementType.Should().Be("Title");
        result[3].Text.Should().Be("PREPARAZIONE");
        result[4].Text.Should().StartWith("Di seguito");
    }

    [Fact]
    public void Split_MultipleEmbeddedTitles_SplitsOnlyTheLeftmost_NonRecursive()
    {
        // Two page-number-boundary headings; only ONE split per element — the leftmost qualifying title
        // is promoted, the second stays inline in the tail.
        var el = Body("Riepilogo. 5 PREPARAZIONE inizia disponendo le tessere sul tabellone. 8 AZIONI disponibili nel turno.");

        var result = EmbeddedTitleSplitter.Split(new[] { el });

        result.Should().HaveCount(3);
        result[1].ElementType.Should().Be("Title");
        result[1].Text.Should().Be("PREPARAZIONE");
        result[2].Text.Should().Contain("AZIONI"); // untouched, remains inline in the tail
        result[2].ElementType.Should().Be("NarrativeText");
    }

    // --- English non-regression: must NOT false-split well-extracted prose ---

    [Fact]
    public void Split_LowercaseSectionWordInProse_DoesNotSplit()
    {
        var el = Body("During setup you may draw a card, and at the end you score points for each tile.");

        var result = EmbeddedTitleSplitter.Split(new[] { el });

        result.Should().ContainSingle();
        result[0].Should().Be(el);
    }

    [Fact]
    public void Split_UppercaseWordPrecededByLetter_TreatedAsInlineProse_DoesNotSplit()
    {
        // Preceding non-space char is a letter ("the ") → inline emphasis, not a heading boundary.
        var el = Body("Follow the SETUP steps carefully before you begin the first round of play.");

        var result = EmbeddedTitleSplitter.Split(new[] { el });

        result.Should().ContainSingle();
        result[0].Should().Be(el);
    }

    [Theory]
    [InlineData("IMPREPARAZIONE del turno adesso descritta in dettaglio qui sotto.")] // glued left
    [InlineData("6 PREPARAZIONER del gioco viene ora descritta nel dettaglio seguente.")] // glued right
    public void Split_NotAWholeWordMatch_DoesNotSplit(string text)
    {
        var result = EmbeddedTitleSplitter.Split(new[] { Body(text) });

        result.Should().ContainSingle();
    }

    [Fact]
    public void Split_UppercaseTitleWithNoTrailingLowercaseProse_DoesNotSplit()
    {
        // A lone ALL-CAPS token or a run of adjacent all-caps headings (no lowercase body within the
        // window) is ambiguous — the conservative guard leaves it alone.
        var el = Body("6 PREPARAZIONE AZIONI GENERAZIONI");

        var result = EmbeddedTitleSplitter.Split(new[] { el });

        result.Should().ContainSingle();
        result[0].Should().Be(el);
    }

    [Theory]
    [InlineData("Fine del paragrafo. SETUP the board using the components listed above now.", "SETUP")]        // sentence terminator
    [InlineData("Leggi il testo a lato. 6 SETUP the board using the components listed on the card.", "SETUP")]  // page number at a clause boundary
    [InlineData("Testo della sezione precedente\nSETUP the board using the components in the manual.", "SETUP")] // line break
    public void Split_StrongHeadingBoundaryVariants_PromoteTitle(string text, string expectedTitle)
    {
        var result = EmbeddedTitleSplitter.Split(new[] { Body(text) });

        result.Should().Contain(e => e.ElementType == "Title" && e.Text == expectedTitle);
    }

    [Theory]
    [InlineData("Nel tuo turno esegui 2 AZIONI a tua scelta dal tabellone principale adesso qui.")]    // digit is a quantifier, not a page number
    [InlineData("Alla fine ricevi 3 PUNTEGGIO bonus per ogni generazione completata nel gioco.")]      // digit quantifier
    [InlineData("Contesto del gioco: AZIONI eseguibili dal giocatore in ogni singolo turno corrente.")] // colon is inline, not a heading boundary
    public void Split_InlineAllCapsAfterQuantifierOrColon_DoesNotFalseSplit(string text)
    {
        // Precision guard (review #3347): common Italian prose "quantifier/label + ALL-CAPS defined term
        // + lowercase" must NOT fabricate a bogus section heading corpus-wide.
        var result = EmbeddedTitleSplitter.Split(new[] { Body(text) });

        result.Should().ContainSingle();
        result[0].ElementType.Should().Be("NarrativeText");
    }

    [Theory]
    [InlineData("Table")]
    [InlineData("ListItem")]
    [InlineData("Title")]
    public void Split_NonProseElementType_IsNotSplit(string elementType)
    {
        // Only prose (NarrativeText/UncategorizedText) is split — never a Table row or a ListItem, which
        // would be torn across a synthetic heading.
        var el = new ExtractedElement("Riepilogo. 6 PREPARAZIONE Di seguito i passi di preparazione del gioco.", 1, elementType);

        var result = EmbeddedTitleSplitter.Split(new[] { el });

        result.Should().ContainSingle();
        result[0].Should().Be(el);
    }

    [Fact]
    public void Split_MultiWordTitle_PrefersLongestAtSamePosition()
    {
        // "GAME SETUP" and "SETUP" both start-of-string; the longest wins so the whole heading is captured.
        var el = Body("GAME SETUP arrange the board and deal starting cards to every player now.");

        var result = EmbeddedTitleSplitter.Split(new[] { el });

        result.Should().Contain(e => e.ElementType == "Title" && e.Text == "GAME SETUP");
        result.Should().NotContain(e => e.ElementType == "Title" && e.Text == "SETUP");
    }

    [Fact]
    public void Split_EmptyList_ReturnsEmpty()
    {
        EmbeddedTitleSplitter.Split(System.Array.Empty<ExtractedElement>()).Should().BeEmpty();
    }

    // --- Header promotion (review dry-run finding: TM's "PREPARAZIONE" is a "Header" element,
    //     not a title glued into body; GroupByTitle ignores "Header", so re-tag lexicon Headers to Title) ---

    private static ExtractedElement Header(string text, int page = 1) => new(text, page, "Header");

    [Theory]
    [InlineData("PREPARAZIONE")]              // the exact TM case
    [InlineData("PANORAMICA DEL GIOCO")]      // multi-word: contains lexicon word "PANORAMICA"
    [InlineData("TESSERE")]                   // component section added to the lexicon in this PR
    [InlineData("PLANCE DEI GIOCATORI")]      // multi-word component section
    public void Split_CleanLexiconHeaderFollowedByBody_IsRetaggedAsTitle(string headerText)
    {
        // A clean ALL-CAPS section title unstructured emitted as Header, not Title, immediately followed
        // by body prose. Re-tagged wholesale (no split); GroupByTitle then opens the section.
        var elements = new[]
        {
            Header(headerText),
            Body("Di seguito viene descritta questa sezione del gioco con i suoi dettagli operativi."),
        };

        var result = EmbeddedTitleSplitter.Split(elements);

        result.Should().HaveCount(2);
        result[0].ElementType.Should().Be("Title");
        result[0].Text.Should().Be(headerText); // whole element re-tagged, text preserved
        result[1].ElementType.Should().Be("NarrativeText");
    }

    [Theory]
    [InlineData("4")]                                        // page number
    [InlineData("*")]                                        // symbol
    [InlineData("London")]                                   // lowercase → not an all-caps title
    [InlineData("DominionRules2021.qxp_WideDominion 8/17/21")] // filename running header (lowercase + digits)
    [InlineData("SETUP p.6")]                                // lexicon word but a page-ref footer: lowercase 'p' + digit
    [InlineData("TERRAFORMING MARS")]                        // all-caps game-title banner, but no lexicon section word
    public void Split_NoisyOrNonLexiconHeader_IsNotPromoted(string headerText)
    {
        // "Header" is dominantly running page-header/footer noise corpus-wide (page numbers, symbols,
        // filenames, city names, game-title banners). Even followed by body, promotion requires a clean
        // all-caps title carrying a curated section word — so noise never fabricates a section.
        var el = Header(headerText);
        var result = EmbeddedTitleSplitter.Split(new[] { el, Body("Testo del corpo che segue l'intestazione rumorosa qui presente ora.") });

        result.Should().HaveCount(2);
        result[0].Should().Be(el); // unchanged, still a Header
    }

    [Fact]
    public void Split_LexiconHeaderWithoutFollowingBody_IsNotPromoted()
    {
        // Bodyless running header (no body element after it) — promoting would yield a heading-only junk
        // chunk that the #3269 IsSubstantial filter cannot drop (a lexicon word is a real >=3-letter run).
        var setupHeader = Header("SETUP");
        var elements = new[] { setupHeader, new ExtractedElement("AZIONI", 1, "Header") };

        var result = EmbeddedTitleSplitter.Split(elements);

        result[0].Should().Be(setupHeader); // not promoted — next element is not body content
    }
}
