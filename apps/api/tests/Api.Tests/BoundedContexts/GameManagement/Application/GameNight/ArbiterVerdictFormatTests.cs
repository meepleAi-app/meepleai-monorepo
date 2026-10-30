using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;
using CitationDto = Api.Models.CitationDto;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #4751 — the text side of the Arbitro: what it is asked, how its answer is read, and the references built
/// from what was retrieved. The model's answer is parsed as the model writes it: markdown headers, a REGOLA line
/// it was not asked for, a COPERTURA it may or may not give.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class ArbiterVerdictFormatTests
{
    // ─── Parse: the sections, in any markdown ─────────────────────────────────

    [Fact]
    public void Parse_WithWellFormedResponse_ParsesVerdictNoteAndCoverage()
    {
        var parsed = ArbiterVerdictFormat.Parse(
            "COPERTURA: SI\nVERDETTO: Giocatore A ha ragione.\nNOTA: In caso di dubbio usare la FAQ.");

        parsed.Verdict.Should().Be("Giocatore A ha ragione.");
        parsed.Note.Should().Be("In caso di dubbio usare la FAQ.");
        parsed.RulebookCoversTheCase.Should().BeTrue();
    }

    [Theory]
    [InlineData("**VERDETTO:** Ha ragione Sara.")]
    [InlineData("**VERDETTO**: Ha ragione Sara.")]
    [InlineData("__VERDETTO:__ Ha ragione Sara.")]
    [InlineData("## VERDETTO: Ha ragione Sara.")]
    [InlineData("### **VERDETTO:** Ha ragione Sara.")]
    [InlineData("- VERDETTO: Ha ragione Sara.")]
    [InlineData("* **VERDETTO:** Ha ragione Sara.")]
    [InlineData("1. VERDETTO: Ha ragione Sara.")]
    [InlineData("> VERDETTO: Ha ragione Sara.")]
    [InlineData("verdetto: Ha ragione Sara.")]
    [InlineData("  Verdetto :  Ha ragione Sara.")]
    public void Parse_RecognisesTheVerdictHeaderWhateverTheMarkdown(string line)
    {
        ArbiterVerdictFormat.Parse(line + "\nNOTA: Nessuna.").Verdict.Should().Be("Ha ragione Sara.");
    }

    [Fact]
    public void Parse_WithBoldHeaders_DropsTheInventedRegolaQuote()
    {
        // The model wrote the headers in bold and quoted a rulebook it does not have. Before, the bold prefix hid
        // every header, the whole text became the verdict, and the quote went to disputes_json and SignalR.
        var parsed = ArbiterVerdictFormat.Parse(
            "**COPERTURA:** SI\n" +
            "**VERDETTO:** Il ladro si sposta subito.\n" +
            "**REGOLA:** Pag. 99, \"Il ladro si sposta subito dopo il lancio\".\n" +
            "**NOTA:** Nessuna.");

        parsed.Verdict.Should().Be("Il ladro si sposta subito.");
        parsed.Note.Should().Be("Nessuna.");
        parsed.RulebookCoversTheCase.Should().BeTrue();
    }

    [Theory]
    [InlineData("Regola: il ladro si sposta subito dopo il sette.")]
    [InlineData("REGOLA: Pag. 99, \"testo inventato\"")]
    [InlineData("**Regola applicabile:** pag. 99")]
    [InlineData("**REGOLA**: pag. 99")]
    [InlineData("- Regola: pag. 99")]
    [InlineData("### REGOLA: pag. 99")]
    [InlineData("REGOLE: pag. 3; pag. 7")]
    [InlineData("Regola del ladro: pag. 99")]
    public void Parse_DropsAnyRegolaLine_InAnyForm_WithoutCuttingTheRest(string regolaLine)
    {
        // A REGOLA line in the middle of the answer is dropped and ends there: the NOTA after it is kept.
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Ha ragione Sara.\n" + regolaLine + "\nNOTA: Nessuna.");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
        parsed.Note.Should().Be("Nessuna.");
    }

    [Fact]
    public void Parse_DropsTheContinuationLinesOfARegolaSection()
    {
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Ha ragione Sara.\n" +
            "REGOLA: Pag. 99,\n" +
            "\"testo inventato\", seconda riga.\n" +
            "NOTA: Nessuna.");

        parsed.Verdict.Should().NotContain("99").And.NotContain("inventato");
        parsed.Note.Should().Be("Nessuna.");
    }

    [Fact]
    public void Parse_AWordInsideALine_IsNotAHeader_AndTruncatesNothing()
    {
        // «regola:» and «nota:» in the middle of a sentence are text, not sections
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Vale la regola: il ladro si sposta subito. Nota: vale per tutti.\n" +
            "NOTA: Nessuna.");

        parsed.Verdict.Should().Be("Vale la regola: il ladro si sposta subito. Nota: vale per tutti.");
        parsed.Note.Should().Be("Nessuna.");
    }

    [Fact]
    public void Parse_AWordThatOnlyStartsLikeAHeader_IsNotOne()
    {
        // «Notare» and «Nota che» begin with NOTA but are not the header
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Ha ragione Sara.\nNotare che il ladro si sposta: subito.\nNota che vale per tutti: sempre.");

        parsed.Verdict.Should().Be("Ha ragione Sara. Notare che il ladro si sposta: subito. Nota che vale per tutti: sempre.");
        parsed.Note.Should().BeNull();
    }

    [Fact]
    public void Parse_AVerdictOnSeveralLines_IsJoined()
    {
        var parsed = ArbiterVerdictFormat.Parse("VERDETTO: Prima parte\nseconda parte.\nNOTA: Una nota\nsu due righe.");

        parsed.Verdict.Should().Be("Prima parte seconda parte.");
        parsed.Note.Should().Be("Una nota su due righe.");
    }

    [Fact]
    public void Parse_StripsTheEmphasisThatClosesAHeaderedLine()
    {
        var parsed = ArbiterVerdictFormat.Parse("**VERDETTO: Ha ragione Sara.**\n**NOTA: Nessuna.**");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
        parsed.Note.Should().Be("Nessuna.");
    }

    // ─── Parse: the answers that are not a verdict ────────────────────────────

    [Fact]
    public void Parse_WithMalformedResponse_UsesFullResponseAsVerdict()
    {
        const string response = "Il giocatore A ha torto perché le regole dicono altro.";

        var parsed = ArbiterVerdictFormat.Parse(response);

        parsed.Verdict.Should().Be(response);
        parsed.Note.Should().BeNull();
        parsed.RulebookCoversTheCase.Should().BeFalse("no COPERTURA line: fail-honest");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Parse_WithEmptyResponse_FailsLoudlyInsteadOfInventingAVerdict(string empty)
    {
        // #4751: it used to return «Nessun verdetto disponibile.», which the handler saved as a real
        // verdict. The handler refuses an empty answer before parsing; reaching this empty is a caller bug.
        var act = () => ArbiterVerdictFormat.Parse(empty);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("VERDETTO:\nREGOLA:\nNOTA:")]
    [InlineData("VERDETTO:")]
    [InlineData("  verdetto:   \n\nregola:  \n")]
    [InlineData("**VERDETTO:**\n**NOTA:**")]
    [InlineData("COPERTURA: SI")]
    [InlineData("REGOLA: pag. 3, «Il ladro»")]
    [InlineData("VERDETTO:\nNOTA: solo una nota")]
    public void Parse_WithNothingButHeadersOrAQuote_YieldsAnEmptyVerdict(string skeleton)
    {
        var parsed = ArbiterVerdictFormat.Parse(skeleton);

        parsed.Verdict.Should().BeEmpty("the bare skeleton, a quote, or a note alone is not a verdict");
    }

    [Fact]
    public void Parse_WithAnUnheadedVerdictAndARegolaSection_KeepsOnlyTheVerdict()
    {
        var parsed = ArbiterVerdictFormat.Parse("Ha ragione Sara.\nREGOLA: pag. 3, «Il ladro»");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
    }

    [Fact]
    public void Parse_WithACoverageLineAndAnUnheadedVerdict_KeepsTheVerdict()
    {
        // COPERTURA is one line: what follows it is not part of it
        var parsed = ArbiterVerdictFormat.Parse("COPERTURA: SI\nHa ragione Sara.");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
        parsed.RulebookCoversTheCase.Should().BeTrue();
    }

    // ─── Parse: COPERTURA ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("COPERTURA: SI", true)]
    [InlineData("COPERTURA: si", true)]
    [InlineData("COPERTURA: Sì", true)]
    [InlineData("COPERTURA: SÌ", true)]
    [InlineData("COPERTURA: yes", true)]
    [InlineData("**COPERTURA:** SI", true)]
    [InlineData("**COPERTURA**: SI", true)]
    [InlineData("COPERTURA: SI, i passaggi coprono il caso", true)]
    [InlineData("COPERTURA: NO", false)]
    [InlineData("COPERTURA: no", false)]
    [InlineData("COPERTURA: Non coperto", false)]
    [InlineData("COPERTURA: Sicuramente no", false)]
    [InlineData("COPERTURA: forse", false)]
    [InlineData("COPERTURA:", false)]
    public void Parse_ReadsTheCoverageDeclaration_AndOnlyAClearYesCounts(string line, bool covers)
    {
        ArbiterVerdictFormat.Parse(line + "\nVERDETTO: Ha ragione Sara.").RulebookCoversTheCase.Should().Be(covers);
    }

    [Fact]
    public void Parse_WithoutACoverageLine_DoesNotCover()
    {
        ArbiterVerdictFormat.Parse("VERDETTO: Ha ragione Sara.\nNOTA: Nessuna.").RulebookCoversTheCase.Should().BeFalse();
    }

    // ─── Copyright markers ────────────────────────────────────────────────────

    [Fact]
    public void StripCopyrightMarkers_RemovesTheMachineSyntaxOnly()
    {
        ArbiterVerdictFormat.StripCopyrightMarkers("VERDETTO: [ref:doc-1:12] Ha ragione [ref:doc-2:3] Sara. [Pagina 4]")
            .Should().Be("VERDETTO: Ha ragione Sara. [Pagina 4]");
    }

    // ─── References ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildReferences_FollowsTheOrderOfTheRetrieval_NotTheScores()
    {
        // RelevanceScore is the score BEFORE the reranker; the list is in the reranker's order
        var references = ArbiterVerdictFormat.BuildReferences(new[]
        {
            Cite(8, 0.20f), Cite(3, 0.60f), Cite(5, 0.30f), Cite(9, 0.95f),
        });

        references.Should().Equal("Fonte, p. 8", "Fonte, p. 3", "Fonte, p. 5");
    }

    [Fact]
    public void BuildReferences_TwoDocumentsOnTheSamePage_AppearOnce()
    {
        var references = ArbiterVerdictFormat.BuildReferences(new[]
        {
            Cite(3, 0.9f, document: "doc-rulebook"),
            Cite(3, 0.8f, document: "doc-expansion"),
            Cite(9, 0.7f, document: "doc-rulebook"),
        });

        references.Should().Equal("Fonte, p. 3", "Fonte, p. 9");
    }

    [Fact]
    public void BuildReferences_DoesNotCallEveryPassageTheRulebook()
    {
        // The retrieval does not say whether a passage is from the rulebook, an expansion or an errata
        ArbiterVerdictFormat.BuildReferences(new[] { Cite(3, 0.9f) }).Single().Should().NotContain("Regolamento");
    }

    [Fact]
    public void BuildReferences_QuotesOnlyAFullTierPassage()
    {
        var references = ArbiterVerdictFormat.BuildReferences(new[]
        {
            new CitationDto("doc-1", 4, 0.9f, "Testo libero del passaggio.", "full"),
            new CitationDto("doc-2", 6, 0.8f, "TESTO PROTETTO CHE NON VA MOSTRATO", "protected"),
            new CitationDto("doc-3", 7, 0.7f, null, "protected", ParaphrasedSnippet: "una parafrasi"),
        });

        references.Should().Equal("Fonte, p. 4: «Testo libero del passaggio.»", "Fonte, p. 6", "Fonte, p. 7");
    }

    [Fact]
    public void BuildReferences_APageThatIsNotKnown_IsCitedWithoutOne()
    {
        ArbiterVerdictFormat.BuildReferences(new[] { Cite(0, 0.9f) }).Should().Equal("Fonte");
    }

    [Fact]
    public void BuildReferences_WithNoPassage_IsEmpty()
    {
        ArbiterVerdictFormat.BuildReferences(Array.Empty<CitationDto>()).Should().BeEmpty();
    }

    private static CitationDto Cite(int page, float score, string document = "doc-rules")
        => new(document, page, score, null, "protected");

    // ─── Notices ──────────────────────────────────────────────────────────────

    [Fact]
    public void NoticeFor_ARulebookVerdict_HasNone()
    {
        ArbiterVerdictFormat.NoticeFor(VerdictBasis.Rulebook).Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(VerdictBasis.NoGame), "non c'è un regolamento di riferimento")]
    [InlineData(nameof(VerdictBasis.OwnershipNotDeclared), "possesso del gioco non dichiarato")]
    [InlineData(nameof(VerdictBasis.RetrievalUnavailable), "non è stato possibile consultare il regolamento")]
    [InlineData(nameof(VerdictBasis.NoPassages), "nel regolamento non ho trovato passaggi pertinenti")]
    [InlineData(nameof(VerdictBasis.NotCovered), "non coprono questo caso")]
    public void NoticeFor_EveryOtherBasis_SaysItsOwnReason_AndThatNothingIsCited(string basisName, string reason)
    {
        // the enum is internal: the case is named, not passed, so that the public test method does not expose it
        var notice = ArbiterVerdictFormat.NoticeFor(Enum.Parse<VerdictBasis>(basisName));

        notice.Should().ContainEquivalentOf(reason);
        notice.Should().Contain("non si basa sul regolamento").And.Contain("nessun riferimento è citato");
    }

    [Fact]
    public void NoticeFor_TheFiveReasonsAreDistinct_SoThatNoneSaysSomethingFalse()
    {
        // «nessun passaggio pertinente» used to open a verdict of a session that had no game at all
        var notices = new[]
        {
            VerdictBasis.NoGame, VerdictBasis.OwnershipNotDeclared, VerdictBasis.RetrievalUnavailable,
            VerdictBasis.NoPassages, VerdictBasis.NotCovered,
        }.Select(b => ArbiterVerdictFormat.NoticeFor(b)!).ToList();

        notices.Should().OnlyHaveUniqueItems();
        ArbiterVerdictFormat.NoGameNotice.Should().NotContain("pertinent");
    }

    // ─── Prompts ──────────────────────────────────────────────────────────────

    private static LiveGameSession StartedSession()
    {
        var session = LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan", TimeProvider.System);
        session.AddPlayer(Guid.NewGuid(), "Alice", PlayerColor.Red, TimeProvider.System, PlayerRole.Host);
        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);
        return session;
    }

    [Fact]
    public void ResponseInstructions_AskForCoverageVerdictAndNote_AndForNoQuote()
    {
        var instructions = ArbiterVerdictFormat.ResponseInstructions(StartedSession());

        instructions.Should().Contain("COPERTURA:").And.Contain("VERDETTO:").And.Contain("NOTA:");
        instructions.Should().NotContain("REGOLA:");
        instructions.Should().NotContainEquivalentOf("citazione esatta");
        instructions.Should().Contain("Catan").And.Contain("Alice");
    }

    [Fact]
    public void DirectPrompt_TellsTheModelThereIsNoRulebook_AndDoesNotAskForCoverage()
    {
        var (system, user) = ArbiterVerdictFormat.DirectPrompt(StartedSession(), "Il ladro si sposta subito?");

        system.Should().ContainEquivalentOf("non hai accesso al regolamento");
        user.Should().Contain("Il ladro si sposta subito?").And.Contain("VERDETTO:").And.Contain("NOTA:");
        (system + user).Should().NotContain("COPERTURA").And.NotContain("REGOLA:");
        (system + user).Should().NotContainEquivalentOf("citazione esatta");
    }
}
