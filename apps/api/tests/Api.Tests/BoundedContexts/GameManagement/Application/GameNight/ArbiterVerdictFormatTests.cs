using System.Diagnostics;
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
/// <remarks>
/// The rule the parser follows, and the tests pin: only the exact labels of the format (VERDETTO, NOTA,
/// COPERTURA, REGOLA) are labels, and only as a whole line. Anything else is text of the verdict — including a
/// line the model invented and left there. A smaller fault than a verdict truncated at it.
/// </remarks>
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
    [InlineData("2) **VERDETTO:** Ha ragione Sara.")]
    [InlineData("> VERDETTO: Ha ragione Sara.")]
    [InlineData("verdetto: Ha ragione Sara.")]
    [InlineData("  Verdetto :  Ha ragione Sara.")]
    public void Parse_RecognisesTheVerdictLabelWhateverTheMarkdownAndCase(string line)
    {
        ArbiterVerdictFormat.Parse(line + "\nNOTA: Nessuna.").Verdict.Should().Be("Ha ragione Sara.");
    }

    [Fact]
    public void Parse_WithBoldLabels_DropsTheInventedRegolaQuote()
    {
        // The model wrote the labels in bold and quoted a rulebook it does not have. Before, the bold prefix hid
        // every label, the whole text became the verdict, and the quote went to disputes_json and SignalR.
        var parsed = ArbiterVerdictFormat.Parse(
            "**COPERTURA:** SI\n" +
            "**VERDETTO:** Il ladro si sposta subito.\n" +
            "**REGOLA:** Pag. 99, \"Il ladro si sposta subito dopo il lancio\".\n" +
            "**NOTA:** Nessuna.");

        parsed.Verdict.Should().Be("Il ladro si sposta subito.");
        parsed.Note.Should().Be("Nessuna.");
        parsed.RulebookCoversTheCase.Should().BeTrue();
    }

    [Fact]
    public void Parse_StripsTheEmphasisThatClosesALabelledLine()
    {
        var parsed = ArbiterVerdictFormat.Parse("**VERDETTO: Ha ragione Sara.**\n**NOTA: Nessuna.**");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
        parsed.Note.Should().Be("Nessuna.");
    }

    [Fact]
    public void Parse_AVerdictOnSeveralLines_IsJoined()
    {
        var parsed = ArbiterVerdictFormat.Parse("VERDETTO: Prima parte\nseconda parte.\nNOTA: Una nota\nsu due righe.");

        parsed.Verdict.Should().Be("Prima parte seconda parte.");
        parsed.Note.Should().Be("Una nota su due righe.");
    }

    // ─── Parse: REGOLA — the exact label is dropped, and only it ──────────────

    [Theory]
    [InlineData("REGOLA: Pag. 99, \"testo inventato\"")]
    [InlineData("Regola: il ladro si sposta subito dopo il sette.")]
    [InlineData("regola : pag. 99")]
    [InlineData("**REGOLA**: pag. 99")]
    [InlineData("**REGOLA:** pag. 99")]
    [InlineData("- Regola: pag. 99")]
    [InlineData("### REGOLA: pag. 99")]
    [InlineData("1. REGOLA: pag. 99")]
    public void Parse_DropsARegolaLine_InAnyMarkdown_AndCutsNothingAround(string regolaLine)
    {
        var parsed = ArbiterVerdictFormat.Parse("VERDETTO: Ha ragione Sara.\n" + regolaLine + "\nNOTA: Nessuna.");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
        parsed.Note.Should().Be("Nessuna.");
    }

    [Fact]
    public void Parse_ARegolaInTheMiddleOfTheVerdict_LeavesTheRestOfTheVerdict()
    {
        // Before, it ended the verdict: the lines after it were thrown away, up to the next label
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Ha ragione Sara.\nREGOLA: pag. 99\nPerciò Marco deve restituire la carta.\nNOTA: Nessuna.");

        parsed.Verdict.Should().Be("Ha ragione Sara. Perciò Marco deve restituire la carta.");
        parsed.Note.Should().Be("Nessuna.");
    }

    [Theory]
    [InlineData("\"testo inventato\", seconda riga.")]
    [InlineData("«testo inventato», seconda riga.")]
    [InlineData("“testo inventato”, seconda riga.")]
    [InlineData("    seconda riga indentata del testo inventato.")]
    [InlineData("\tseconda riga con tabulazione.")]
    public void Parse_DropsTheQuotedOrIndentedLinesRightAfterARegola(string continuation)
    {
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Ha ragione Sara.\nREGOLA: Pag. 99,\n" + continuation + "\nNOTA: Nessuna.");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
        parsed.Note.Should().Be("Nessuna.");
    }

    [Fact]
    public void Parse_ARegolaBeforeTheVerdict_IsDroppedWithItsQuotedContinuation()
    {
        var parsed = ArbiterVerdictFormat.Parse(
            "REGOLA: Pag. 99,\n\"inventato\"\nVERDETTO: Sara ha ragione.\nNOTA: x");

        parsed.Verdict.Should().Be("Sara ha ragione.");
        parsed.Note.Should().Be("x");
    }

    [Fact]
    public void Parse_AnUnquotedUnindentedLineAfterARegola_IsTheVerdict_NotPartOfTheRegola()
    {
        // Documented, and chosen: a line the model invented and left behind is a smaller fault than a verdict
        // cut short, and a continuation without quotation marks or indent cannot be told from the verdict.
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Ha ragione Sara.\nREGOLA: Pag. 99,\nseconda riga senza virgolette né rientro.\nNOTA: Nessuna.");

        parsed.Verdict.Should().Be("Ha ragione Sara. seconda riga senza virgolette né rientro.");
    }

    // ─── Parse: what looks like a label and is not one ────────────────────────

    [Fact]
    public void Parse_ALineThatStartsLikeARegolaButIsNotTheLabel_IsTheVerdict_AndCutsNothing()
    {
        // Executed by the reviewer: the verdict was saved as «Ha ragione Sara.», «Perciò…» lost
        var parsed = ArbiterVerdictFormat.Parse(
            "COPERTURA: SI\n" +
            "VERDETTO: Ha ragione Sara.\n" +
            "Regola del ladro: si sposta subito dopo il sette.\n" +
            "Perciò Marco deve restituire la carta.\n" +
            "NOTA: Nessuna.");

        parsed.Verdict.Should().Be(
            "Ha ragione Sara. Regola del ladro: si sposta subito dopo il sette. Perciò Marco deve restituire la carta.");
        parsed.Note.Should().Be("Nessuna.");
    }

    [Fact]
    public void Parse_ABulletedVerdictWithARegolaGeneraleItem_KeepsEveryItem()
    {
        var parsed = ArbiterVerdictFormat.Parse(
            "COPERTURA: SI\n" +
            "VERDETTO:\n" +
            "- Sara ha ragione.\n" +
            "- Regola generale: il ladro si sposta subito.\n" +
            "- Marco restituisce la carta.\n" +
            "NOTA: Nessuna.");

        parsed.Verdict.Should().Be(
            "- Sara ha ragione. - Regola generale: il ladro si sposta subito. - Marco restituisce la carta.");
    }

    [Fact]
    public void Parse_AColonAtTheEndOfASentence_FollowedByRegoleLines_DoesNotEndTheVerdict()
    {
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Ha ragione Sara perché valgono queste due condizioni:\n" +
            "Regole di movimento: il ladro si sposta.\n" +
            "Regole di furto: si ruba una carta.\n" +
            "NOTA: x");

        parsed.Verdict.Should().Be(
            "Ha ragione Sara perché valgono queste due condizioni: Regole di movimento: il ladro si sposta. " +
            "Regole di furto: si ruba una carta.");
        parsed.Note.Should().Be("x");
    }

    [Theory]
    [InlineData("Regola applicabile al nostro caso: pag. 99, \"testo inventato\".")]
    [InlineData("Regola applicabile: pag. 99")]
    [InlineData("Regole: pag. 3; pag. 7")]
    [InlineData("**Regola applicabile:** pag. 99")]
    [InlineData("Regola che vale per tutti i giocatori seduti al tavolo: il ladro si sposta subito.")]
    [InlineData("Regolamento: pag. 99")]
    [InlineData("Nota che vale per tutti: sempre.")]
    [InlineData("Notare che il ladro si sposta: subito.")]
    [InlineData("Verdetto finale: Sara.")]
    public void Parse_OnlyTheExactLabelsAreLabels_EverythingElseIsTheVerdict(string notALabel)
    {
        // Documents what happens to the forms that are NOT the label — a «Regola applicabile…:» the model invented
        // included: it stays in the verdict. Cutting it would cut text that cannot be told apart from a verdict.
        var parsed = ArbiterVerdictFormat.Parse("VERDETTO: Ha ragione Sara.\n" + notALabel + "\nNOTA: x");

        parsed.Verdict.Should().Be("Ha ragione Sara. " + notALabel);
        parsed.Note.Should().Be("x");
    }

    [Fact]
    public void Parse_ALabelInsideASentence_IsNotALabel_AndTruncatesNothing()
    {
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Vale la regola: il ladro si sposta subito. Nota: vale per tutti.\nNOTA: Nessuna.");

        parsed.Verdict.Should().Be("Vale la regola: il ladro si sposta subito. Nota: vale per tutti.");
        parsed.Note.Should().Be("Nessuna.");
    }

    [Fact]
    public void Parse_ACoperturaLineInTheMiddleOfTheVerdict_IsRead_AndLeavesTheVerdictWhole()
    {
        // Executed by the reviewer: it used to cut the verdict at it
        var parsed = ArbiterVerdictFormat.Parse(
            "VERDETTO: Ha ragione Sara perché\nCopertura: SI\nil ladro si sposta subito.\nNOTA: x");

        parsed.Verdict.Should().Be("Ha ragione Sara perché il ladro si sposta subito.");
        parsed.RulebookCoversTheCase.Should().BeTrue();
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
    public void Parse_WithNothingButLabelsOrAQuote_YieldsAnEmptyVerdict(string skeleton)
    {
        var parsed = ArbiterVerdictFormat.Parse(skeleton);

        parsed.Verdict.Should().BeEmpty("the bare skeleton, a quote, or a note alone is not a verdict");
    }

    [Fact]
    public void Parse_WithAnUnlabelledVerdictAndARegolaLine_KeepsOnlyTheVerdict()
    {
        var parsed = ArbiterVerdictFormat.Parse("Ha ragione Sara.\nREGOLA: pag. 3, «Il ladro»");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
    }

    [Fact]
    public void Parse_WithACoverageLineAndAnUnlabelledVerdict_KeepsTheVerdict()
    {
        // COPERTURA is one line: what follows it is not part of it
        var parsed = ArbiterVerdictFormat.Parse("COPERTURA: SI\nHa ragione Sara.");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
        parsed.RulebookCoversTheCase.Should().BeTrue();
    }

    // ─── Parse: COPERTURA — «yes» only when it is exactly yes ─────────────────

    [Theory]
    [InlineData("SI")]
    [InlineData("si")]
    [InlineData("Si")]
    [InlineData("SÌ")]
    [InlineData("Sì")]
    [InlineData("sì")]
    [InlineData("SI.")]
    [InlineData("SI!")]
    [InlineData("  SI  ")]
    [InlineData("**SI**")]
    [InlineData("`SI`")]
    [InlineData("__Sì__")]
    public void Parse_ACoverageOfExactlyYes_Covers(string value)
    {
        ArbiterVerdictFormat.Parse($"COPERTURA: {value}\nVERDETTO: Ha ragione Sara.").RulebookCoversTheCase
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("NO")]
    [InlineData("no")]
    [InlineData("Non coperto")]
    [InlineData("forse")]
    [InlineData("")]
    [InlineData("SÍ")] // acute accent: not the Italian word
    [InlineData("Y")]
    [InlineData("YES")]
    [InlineData("yes")]
    [InlineData("S")]
    [InlineData("SI/NO")]
    [InlineData("SI o NO")]
    [InlineData("SI se i passaggi coprono; NO se non coprono")]
    [InlineData("[SI se i passaggi ... NO se non lo coprono]")]
    [InlineData("[SI]")]
    [InlineData("Sì, ma solo in parte")]
    [InlineData("SI (parziale)")]
    [InlineData("si può dire di sì")]
    [InlineData("NO, ma SI in parte")]
    [InlineData("Sicuramente")]
    public void Parse_AnyOtherCoverage_DoesNotCover(string value)
    {
        // The prompt says an ambiguous answer is a «no»: it used to count «SI/NO», «SI poi NO» and
        // «Sì, ma solo in parte» as «yes». The verdict then cited passages on the strength of an ambiguity.
        ArbiterVerdictFormat.Parse($"COPERTURA: {value}\nVERDETTO: Ha ragione Sara.").RulebookCoversTheCase
            .Should().BeFalse();
    }

    [Theory]
    [InlineData("COPERTURA: SI\nVERDETTO: Sara.\nCOPERTURA: NO\nNOTA: x")]
    [InlineData("COPERTURA: NO\nVERDETTO: Sara.\nCOPERTURA: SI\nNOTA: x")]
    [InlineData("COPERTURA: SI\nVERDETTO: Sara.\nCOPERTURA: SI\nNOTA: x")]
    public void Parse_MoreThanOneCoverageLine_DoesNotCover(string answer)
    {
        // Two lines contradict each other, or repeat: neither is «a clear yes»
        ArbiterVerdictFormat.Parse(answer).RulebookCoversTheCase.Should().BeFalse();
    }

    [Fact]
    public void Parse_WithoutACoverageLine_DoesNotCover()
    {
        ArbiterVerdictFormat.Parse("VERDETTO: Ha ragione Sara.\nNOTA: Nessuna.").RulebookCoversTheCase.Should().BeFalse();
    }

    // ─── Parse: lines no regular expression should be asked about ─────────────

    [Theory]
    [InlineData('*')]
    [InlineData('-')]
    [InlineData('_')]
    [InlineData('#')]
    [InlineData(' ')]
    [InlineData('>')]
    public void Parse_ADegenerateLine_IsReadInLinearTime(char c)
    {
        // The model's answer is not a thing to hand to a pattern that can backtrack: a line of tens of thousands
        // of the same decoration character used to be quadratic. The bound is generous; the failure it catches
        // is orders of magnitude over it.
        var line = new string(c, 60_000);
        var stopwatch = Stopwatch.StartNew();

        var parsed = ArbiterVerdictFormat.Parse("VERDETTO: ok\n" + line + "\nNOTA: x");

        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
        parsed.Note.Should().Be("x");
    }

    [Fact]
    public void Parse_AHeadedLineWithAVeryLongEmphasisRun_IsReadInLinearTime()
    {
        var stopwatch = Stopwatch.StartNew();

        var parsed = ArbiterVerdictFormat.Parse("VERDETTO: " + new string('*', 60_000) + "x");

        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
        parsed.Verdict.Should().EndWith("x");
    }

    // ─── Copyright markers ────────────────────────────────────────────────────

    [Fact]
    public void StripCopyrightMarkers_RemovesTheMachineSyntaxOnly()
    {
        ArbiterVerdictFormat.StripCopyrightMarkers("VERDETTO: [ref:doc-1:12] Ha ragione [ref:doc-2:3] Sara. [Pagina 4]")
            .Should().Be("VERDETTO: Ha ragione Sara. [Pagina 4]");
    }

    [Theory]
    [InlineData("[ref:doc-1:12] Ha ragione Sara.", "Ha ragione Sara.")]
    [InlineData("Ha ragione Sara. [ref:doc-1:12]", "Ha ragione Sara.")]
    [InlineData("Ha ragione[ref:doc-1:12]Sara.", "Ha ragione Sara.")]
    [InlineData("A\n[ref:doc-1:12] B\nC [ref:doc-1:12]\nD", "A\nB\nC\nD")]
    [InlineData("[ref:a:1][ref:b:2] testo", "testo")]
    [InlineData("[ref:doc-1:12]", "")]
    public void StripCopyrightMarkers_TidiesTheSpacesAroundTheMarker(string text, string expected)
    {
        ArbiterVerdictFormat.StripCopyrightMarkers(text).Should().Be(expected);
    }

    [Theory]
    [InlineData("[ref:]")]
    [InlineData("[ref:doc]")]
    [InlineData("[ref:doc:]")]
    [InlineData("[ref:doc:x]")]
    [InlineData("[ref:doc:12")]
    [InlineData("[ref::12]")]
    [InlineData("[ref: nota]")]
    public void StripCopyrightMarkers_LeavesWhatIsNotAMarker(string text)
    {
        ArbiterVerdictFormat.StripCopyrightMarkers(text).Should().Be(text);
    }

    [Fact]
    public void StripCopyrightMarkers_OnVeryLongInput_IsLinear()
    {
        var spaces = new string(' ', 60_000);
        var openings = string.Concat(Enumerable.Repeat("[ref:", 20_000));
        var stopwatch = Stopwatch.StartNew();

        var stripped = ArbiterVerdictFormat.StripCopyrightMarkers(spaces + "[ref:d:1]" + spaces);
        var unmatched = ArbiterVerdictFormat.StripCopyrightMarkers(openings);

        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
        stripped.Should().NotContain("[ref");
        unmatched.Should().Be(openings);
    }

    // ─── References ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildReferences_KeepsTheOrderItIsGiven_NotTheOrderOfTheScores()
    {
        // The method does not sort. What KnowledgeBase hands over is already in an order (after the sentence
        // window, by the score from before the rerank), and this keeps it: scores here are not sorted, and sorting
        // by them would put page 9 first.
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
    public void BuildReferences_AFullPassageFromAPdf_HasItsLineBreaksAndSpacesCollapsed()
    {
        // The preview is the start of a chunk of a PDF: line breaks and runs of spaces end up, raw, in
        // disputes_json and in what every player receives. They are collapsed; nothing is cut.
        var references = ArbiterVerdictFormat.BuildReferences(new[]
        {
            new CitationDto("doc-1", 4, 0.9f, "  Ogni giocatore\npuò  giocare\r\n\tfino a due carte.\n", "full"),
        });

        references.Should().Equal("Fonte, p. 4: «Ogni giocatore può giocare fino a due carte.»");
    }

    [Fact]
    public void BuildReferences_AFullPassage_IsNotTruncated()
    {
        var text = string.Join(" ", Enumerable.Repeat("parola", 120));

        var reference = ArbiterVerdictFormat.BuildReferences(new[] { new CitationDto("doc-1", 4, 0.9f, text, "full") })
            .Single();

        reference.Should().Be($"Fonte, p. 4: «{text}»", "the length is KnowledgeBase's to decide, not this method's");
    }

    [Fact]
    public void BuildReferences_AFullPassageThatIsOnlyWhitespace_IsCitedByPage()
    {
        ArbiterVerdictFormat.BuildReferences(new[] { new CitationDto("doc-1", 4, 0.9f, " \n\t ", "full") })
            .Should().Equal("Fonte, p. 4");
    }

    [Fact]
    public void BuildReferences_APageThatIsNotKnown_IsCitedWithoutOne()
    {
        ArbiterVerdictFormat.BuildReferences(new[] { Cite(0, 0.9f) }).Should().Equal("Fonte");
    }

    // #4782 — a document indexed without the page of each passage claims page 1 for all of them. Better no page
    // than a false one.
    [Fact]
    public void BuildReferences_APassageOfADocumentWhosePagesAreNotBelieved_IsCitedWithoutThePage()
    {
        var references = ArbiterVerdictFormat.BuildReferences(
            new[] { Cite(1, 0.9f, "doc-flat"), Cite(4, 0.8f, "doc-ok") },
            new HashSet<string> { "doc-flat" });

        references.Should().Equal("Fonte", "Fonte, p. 4");
    }

    [Fact]
    public void BuildReferences_TwoPassagesOfADocumentWithoutPages_AppearOnce()
    {
        var references = ArbiterVerdictFormat.BuildReferences(
            new[] { Cite(1, 0.9f, "doc-flat"), Cite(1, 0.8f, "doc-flat"), Cite(3, 0.7f, "doc-flat") },
            new HashSet<string> { "doc-flat" });

        references.Should().Equal("Fonte");
    }

    [Fact]
    public void BuildReferences_AFullPassageOfADocumentWithoutPages_KeepsItsTextAndLosesThePage()
    {
        var references = ArbiterVerdictFormat.BuildReferences(
            new[] { new CitationDto("doc-flat", 1, 0.9f, "Ogni giocatore pesca due carte.", "full") },
            new HashSet<string> { "doc-flat" });

        references.Should().Equal("Fonte: «Ogni giocatore pesca due carte.»");
    }

    [Fact]
    public void BuildReferences_WithNoDocumentWithoutPages_CitesEveryPageItCarries()
    {
        var citations = new[] { Cite(1, 0.9f, "doc-1"), Cite(4, 0.8f, "doc-2") };

        ArbiterVerdictFormat.BuildReferences(citations, new HashSet<string>()).Should().Equal("Fonte, p. 1", "Fonte, p. 4");
        ArbiterVerdictFormat.BuildReferences(citations, null).Should().Equal("Fonte, p. 1", "Fonte, p. 4");
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
    [InlineData(nameof(VerdictBasis.CopyrightWithheld), "non può riportare il testo del regolamento")]
    public void NoticeFor_EveryOtherBasis_SaysItsOwnReason_AndThatNothingIsCited(string basisName, string reason)
    {
        // the enum is internal: the case is named, not passed, so that the public test method does not expose it
        var notice = ArbiterVerdictFormat.NoticeFor(Enum.Parse<VerdictBasis>(basisName));

        notice.Should().ContainEquivalentOf(reason);
        notice.Should().Contain("non si basa sul regolamento").And.Contain("nessun riferimento è citato");
    }

    [Fact]
    public void NoticeFor_TheReasonsAreDistinct_SoThatNoneSaysSomethingFalse()
    {
        // «nessun passaggio pertinente» used to open the verdict of a session that had no game at all
        var notices = Enum.GetValues<VerdictBasis>()
            .Where(b => b != VerdictBasis.Rulebook)
            .Select(b => ArbiterVerdictFormat.NoticeFor(b)!)
            .ToList();

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
    public void ResponseInstructions_TellTheModelToWriteOnlyTheWord_OnTheCoverageLine()
    {
        // The parser takes «yes» only from exactly SI/SÌ, so «COPERTURA: SI, i passaggi coprono il caso» is a «no»
        // (notice, no references, even if the model did use the passages). The prompt must say what the parser
        // wants — and its own example must not show a line the parser would refuse.
        var instructions = ArbiterVerdictFormat.ResponseInstructions(StartedSession());

        instructions.Should().Contain("SOLO la parola SI oppure la parola NO, senza altro testo");
        instructions.Should().Contain("la spiegazione va nel VERDETTO");
    }

    [Fact]
    public void ResponseInstructions_EveryCoverageLineOfTheFormat_IsExactlyTheWordAndNothingAfterIt()
    {
        var instructions = ArbiterVerdictFormat.ResponseInstructions(StartedSession());

        var coverageLines = instructions
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("COPERTURA:", StringComparison.Ordinal))
            .ToList();

        coverageLines.Should().NotBeEmpty("the format shows the line");
        coverageLines.Should().OnlyContain(
            line => line == "COPERTURA: SI" || line == "COPERTURA: NO",
            "an example the model may copy must be a line the parser accepts, with nothing after the word");
        instructions.Should().NotContain("[SI se", "the old example put the explanation on the line itself");
    }

    [Fact]
    public void ResponseInstructions_TheExampleOfTheCoverageLine_IsTheReadableOne()
    {
        // What the prompt shows, the parser reads: the example line is a valid answer, not an echo it would refuse
        var instructions = ArbiterVerdictFormat.ResponseInstructions(StartedSession());
        var example = instructions
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .First(line => line.StartsWith("COPERTURA:", StringComparison.Ordinal));

        var parsed = ArbiterVerdictFormat.Parse(example + "\nVERDETTO: Ha ragione Sara.");

        parsed.Verdict.Should().Be("Ha ragione Sara.");
        parsed.RulebookCoversTheCase.Should().Be(example == "COPERTURA: SI");
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
