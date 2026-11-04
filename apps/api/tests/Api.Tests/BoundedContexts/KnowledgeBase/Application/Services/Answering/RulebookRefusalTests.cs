using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #3850, #4481: il riconoscimento della rinuncia e il suo testo nella lingua della risposta.
/// Le varianti vengono dalle risposte vere della misura del 2026-10-18
/// (<c>tests/evaluation-datasets/misure/2026-10-18-*.json</c>): la frase inglese da sola, in coda a
/// una risposta in italiano, dopo due punti, seguita da una parentesi.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class RulebookRefusalTests
{
    private const string English = "This information is not available in the provided rulebook.";
    private const string Italian = "Questa informazione non è disponibile nel regolamento fornito.";

    [Theory]
    [InlineData(English)]
    [InlineData("This information is not available in the provided rulebook")]
    [InlineData("  This information is not available in the provided rulebook.\n")]
    [InlineData("'This information is not available in the provided rulebook.'")]
    [InlineData("\"This information is not available in the provided rulebook.\"")]
    [InlineData("«This information is not available in the provided rulebook.»")]
    [InlineData("**This information is not available in the provided rulebook.**")]
    [InlineData("this information is NOT available in the provided rulebook.")]
    [InlineData("This  information is not\navailable in the provided   rulebook.")]
    [InlineData("This information is not available in the provided rulebook!")]
    public void IsRefusal_TheModelSentence_WithSpacesQuotesAndPunctuation(string answer)
    {
        RulebookRefusal.IsRefusal(answer).Should().BeTrue();
    }

    [Theory]
    [InlineData(Italian)]
    [InlineData("Diese Information ist im bereitgestellten Regelheft nicht enthalten.")]
    [InlineData("Cette information n'est pas disponible dans le livret de règles fourni.")]
    [InlineData("Cette information n’est pas disponible dans le livret de règles fourni.")]
    [InlineData("Esta información no está disponible en el reglamento proporcionado.")]
    public void IsRefusal_TheLocalizedTexts_SoACachedRefusalIsStillARefusal(string answer)
    {
        RulebookRefusal.IsRefusal(answer).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Sì, puoi scambiare risorse con il giocatore di turno [Page 1].")]
    [InlineData("This information is available on page 3.")]
    [InlineData("This information is not available in the provided rulebooks.")]
    // CAT-20: una risposta in italiano con la rinuncia in coda è una risposta, non una rinuncia.
    [InlineData("- La regola non specifica alcun requisito [Page 1].\n\nThis information is not available in the provided rulebook.")]
    // GLO-16: una frase propria prima della rinuncia; non la si riconosce come rinuncia intera.
    [InlineData("No puedo responder a esa pregunta con la información del contexto proporcionado.\n\nThis information is not available in the provided rulebook.")]
    public void IsRefusal_AnythingElse_IsNotARefusal(string? answer)
    {
        RulebookRefusal.IsRefusal(answer).Should().BeFalse();
    }

    [Theory]
    [InlineData("it", Italian)]
    [InlineData("IT", Italian)]
    [InlineData("it-IT", Italian)]
    [InlineData("en", English)]
    [InlineData("de", "Diese Information ist im bereitgestellten Regelheft nicht enthalten.")]
    [InlineData("fr", "Cette information n'est pas disponible dans le livret de règles fourni.")]
    [InlineData("es", "Esta información no está disponible en el reglamento proporcionado.")]
    [InlineData("pt", English)]
    [InlineData(null, English)]
    public void TextFor_TheLanguageOfTheAnswer_OrEnglish(string? language, string expected)
    {
        RulebookRefusal.TextFor(language).Should().Be(expected);
    }

    [Theory]
    [InlineData("it", "Nel regolamento non ho trovato informazioni pertinenti.")]
    [InlineData("en", "No relevant information found in the rulebook.")]
    [InlineData("xx", "No relevant information found in the rulebook.")]
    public void NoResultsTextFor_TheLanguageOfTheAnswer_OrEnglish(string language, string expected)
    {
        RulebookRefusal.NoResultsTextFor(language).Should().Be(expected);
    }

    [Fact]
    public void Localize_TheWholeRefusal_BecomesTheTextOfTheLanguage()
    {
        RulebookRefusal.Localize(English, "it").Should().Be(Italian);
    }

    [Fact]
    public void Localize_ARefusalInTail_ReplacesOnlyTheSentence()
    {
        // CAT-20, CAR-24, GLO-20, GLO-28: la risposta resta, la frase in coda cambia lingua.
        const string answer = "- Quando muovi il Brigante, puoi rubare 1 carta [Page 1].\n\n" + English;

        RulebookRefusal.Localize(answer, "it")
            .Should().Be("- Quando muovi il Brigante, puoi rubare 1 carta [Page 1].\n\n" + Italian);
    }

    [Fact]
    public void Localize_AfterAColon_AndBeforeAParenthesis()
    {
        // CAT-18 e TTR-17.
        RulebookRefusal.Localize("nel calcolo della Strada più Lunga: " + English, "it")
            .Should().Be("nel calcolo della Strada più Lunga: " + Italian);
        RulebookRefusal.Localize(English + " (per una regola esplicita non c'è una frase dedicata)", "it")
            .Should().Be(Italian + " (per una regola esplicita non c'è una frase dedicata)");
    }

    [Fact]
    public void Localize_KeepsQuotesAndThePunctuationOfTheModel()
    {
        RulebookRefusal.Localize("'This information is not available in the provided rulebook'", "it")
            .Should().Be("'Questa informazione non è disponibile nel regolamento fornito'");
    }

    [Fact]
    public void Localize_EveryOccurrence_WhateverTheCaseAndSpacing()
    {
        RulebookRefusal.Localize("this information is not  available in the provided rulebook. " + English, "it")
            .Should().Be("questa informazione non è disponibile nel regolamento fornito. " + Italian);
    }

    [Theory]
    [InlineData("nel calcolo della Strada più Lunga: this information is not available in the provided rulebook.",
        "nel calcolo della Strada più Lunga: questa informazione non è disponibile nel regolamento fornito.")]
    [InlineData("Vale 2 PV, ma this information is not available in the provided rulebook.",
        "Vale 2 PV, ma questa informazione non è disponibile nel regolamento fornito.")]
    [InlineData("Vale 2 PV. This information is not available in the provided rulebook.",
        "Vale 2 PV. Questa informazione non è disponibile nel regolamento fornito.")]
    public void Localize_FollowsTheCaseOfTheFirstLetterTheModelWrote(string answer, string expected)
    {
        // Review di #4494: dopo «…più Lunga:» o «ma» la frase non comincia con la maiuscola.
        RulebookRefusal.Localize(answer, "it").Should().Be(expected);
    }

    /// <summary>
    /// Review di #4494: il riconoscimento vale finché il prompt del profilo chiede questa frase. Il
    /// prompt della risposta sulle regole lo scrive solo la migrazione che crea il profilo; le
    /// pubblicazioni successive copiano il contenuto della versione pubblicata e cambiano soltanto
    /// la sezione dell'assistente in partita. Se qualcuno cambia la frase nel prompt, questo test lo dice.
    /// </summary>
    [Fact]
    public void ModelSentence_IsTheSentenceThePublishedProfilePromptAsksFor()
    {
        var prompts = new Api.Infrastructure.Migrations.AddAgentProfile().UpOperations
            .OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>()
            .Select(o => o.Sql)
            .Where(sql => sql.Contains("\"systemPrompts\"", StringComparison.Ordinal))
            .ToList();

        prompts.Should().ContainSingle();
        prompts[0].Should().Contain($"respond EXACTLY with: '{RulebookRefusal.ModelSentence}'");
        RulebookRefusal.IsRefusal(RulebookRefusal.ModelSentence).Should().BeTrue();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("pt")]
    public void Localize_WhenTheLanguageIsEnglishOrHasNoText_LeavesTheAnswerAlone(string language)
    {
        RulebookRefusal.Localize(English, language).Should().Be(English);
    }

    [Theory]
    [InlineData("Sì, puoi scambiare risorse [Page 1].")]
    [InlineData("This information is available on page 3.")]
    [InlineData("This information is not available in the provided rulebooks.")]
    public void Localize_AnAnswerWithoutTheSentence_IsUnchanged(string answer)
    {
        RulebookRefusal.Localize(answer, "it").Should().Be(answer);
    }
}
