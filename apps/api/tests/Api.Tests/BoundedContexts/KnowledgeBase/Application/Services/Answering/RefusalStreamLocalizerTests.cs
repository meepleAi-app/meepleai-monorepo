using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #3850: sullo stream la rinuncia arriva a pezzi. Ciò che l'utente vede non deve mai contenere la
/// frase inglese, e una risposta che comincia come la frase non va trattenuta per sempre né persa.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class RefusalStreamLocalizerTests
{
    private const string English = "This information is not available in the provided rulebook.";
    private const string Italian = "Questa informazione non è disponibile nel regolamento fornito.";

    private static readonly string[] RefusalInTokens =
        ["This", " information", " is", " not", " avail", "able", " in the", " provided", " rule", "book", "."];

    /// <summary>Ciò che l'utente vede: ogni pezzo emesso, nell'ordine, e il resto a fine stream.</summary>
    private static List<string> Stream(RefusalStreamLocalizer localizer, IEnumerable<string> chunks)
    {
        var shown = chunks.Select(localizer.Push).Where(s => s.Length > 0).ToList();
        var rest = localizer.Complete();
        if (rest.Length > 0)
        {
            shown.Add(rest);
        }

        return shown;
    }

    [Fact]
    public void TheRefusalInPieces_ReachesTheUserInItalian_WithoutAnEnglishWord()
    {
        var localizer = new RefusalStreamLocalizer("it");

        var shown = Stream(localizer, RefusalInTokens);

        string.Concat(shown).Should().Be(Italian);
        shown.Should().NotContain(s => s.Contains("information", StringComparison.OrdinalIgnoreCase)
            || s.Contains("This", StringComparison.Ordinal) || s.Contains("rulebook", StringComparison.Ordinal));
        localizer.Delivered.Should().Be(Italian);
    }

    [Fact]
    public void TheRefusalOneCharacterAtATime_IsStillReplaced()
    {
        var localizer = new RefusalStreamLocalizer("it");

        var shown = Stream(localizer, English.Select(c => c.ToString()));

        string.Concat(shown).Should().Be(Italian);
        shown.Should().NotContain(s => s.Contains('T'));
    }

    [Fact]
    public void TheRefusalWithoutFinalPeriod_IsReplacedWhenTheStreamEnds()
    {
        var localizer = new RefusalStreamLocalizer("it");

        var shown = Stream(localizer, RefusalInTokens[..^1]);

        string.Concat(shown).Should().Be("Questa informazione non è disponibile nel regolamento fornito");
    }

    [Fact]
    public void TheRefusalInTail_ReplacesTheTail_AndTheAnswerFlowsAsItComes()
    {
        var localizer = new RefusalStreamLocalizer("it");

        // La risposta arriva senza attese: nessun pezzo comincia come la frase.
        localizer.Push("- La tessera vale **2 PV**").Should().Be("- La tessera vale **2 PV**");
        localizer.Push(" [Page 8].\n\n").Should().Be(" [Page 8].\n\n");

        var shown = Stream(localizer, RefusalInTokens);

        string.Concat(shown).Should().Be(Italian);
        localizer.Delivered.Should().Be("- La tessera vale **2 PV** [Page 8].\n\n" + Italian);
    }

    [Fact]
    public void AnAnswerThatStartsLikeTheSentence_IsReleasedWhereItDiverges()
    {
        var localizer = new RefusalStreamLocalizer("it");

        localizer.Push("This").Should().BeEmpty("could still be the refusal");
        localizer.Push(" information").Should().BeEmpty();
        localizer.Push(" is").Should().BeEmpty();
        localizer.Push(" on page 3").Should().Be("This information is on page 3",
            "the text is no longer the sentence: what was held goes out at once");
        localizer.Push(".").Should().Be(".");
        localizer.Complete().Should().BeEmpty();
        localizer.Delivered.Should().Be("This information is on page 3.");
    }

    [Fact]
    public void AStreamThatEndsInTheMiddleOfTheSentence_IsNotLost()
    {
        var localizer = new RefusalStreamLocalizer("it");

        localizer.Push("Vedi sopra. This information").Should().Be("Vedi sopra. ");
        localizer.Complete().Should().Be("This information");
        localizer.Delivered.Should().Be("Vedi sopra. This information");
    }

    [Fact]
    public void AWordThatOnlyEndsLikeTheStartOfTheSentence_IsNotHeld()
    {
        var localizer = new RefusalStreamLocalizer("it");

        localizer.Push("Tutti").Should().Be("Tutti");
        localizer.Push(" i giocatori pescano at").Should().Be(" i giocatori pescano at");
    }

    [Fact]
    public void InAnEnglishConversation_EveryPieceGoesOutAsItArrives()
    {
        var localizer = new RefusalStreamLocalizer("en");

        foreach (var token in RefusalInTokens)
        {
            localizer.Push(token).Should().Be(token);
        }

        localizer.Complete().Should().BeEmpty();
        localizer.Delivered.Should().Be(English);
    }
}
