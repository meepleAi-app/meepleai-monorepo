using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// #4435: la lingua inviata a Unstructured viene dal documento, non dal letterale <c>"ita"</c>.
/// Precedenza: <c>language_override</c>, poi <c>Language</c> se <c>language_confidence</c> supera la
/// soglia, altrimenti l'insieme di configurazione (<c>PdfProcessing:Extractor:Unstructured:Language</c>).
/// La lingua del documento non viaggia mai da sola (review di #4515): va PER PRIMA, seguita dal resto
/// dell'insieme. Tesseract usa la prima come primaria e ricorre alle altre parola per parola, quindi un
/// PDF italiano con i crediti inglesi in testa, rilevato "en", conserva l'italiano. Una lingua fuori
/// dall'insieme dà l'insieme così com'è.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4435")]
public sealed class UnstructuredLanguageResolverTests
{
    private static UnstructuredOptions Options(string language = "eng+ita+deu", double threshold = 0.8) =>
        new() { Language = language, LanguageConfidenceThreshold = threshold };

    [Fact]
    public void Override_De_SendsDeuFirst_EvenWhenDetectionSaysEnglish()
    {
        var hint = new DocumentLanguageHint(LanguageOverride: "de", DetectedLanguage: "en", DetectedConfidence: 0.99);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be("deu+eng+ita");
    }

    [Fact]
    public void Override_IsCaseAndRegionInsensitive()
    {
        var hint = new DocumentLanguageHint(LanguageOverride: " DE-at ", DetectedLanguage: null, DetectedConfidence: null);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be("deu+eng+ita");
    }

    [Fact]
    public void Detected_AboveThreshold_SendsDetectedLanguageFirst()
    {
        var hint = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "it", DetectedConfidence: 0.95);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be("ita+eng+deu");
    }

    [Theory]
    [InlineData("en", "eng+ita+deu")]
    [InlineData("it", "ita+eng+deu")]
    [InlineData("de", "deu+eng+ita")]
    public void DocumentLanguage_IsNeverSentAlone_WhenTheSetHasOthers(string language, string expected)
    {
        // Il rilevatore legge i primi 2000 caratteri e la sua confidenza è la quota di lettere latine:
        // vale ~1 per ogni testo latino, quindi su un PDF Ready la soglia non filtra quasi mai. Una sola
        // lingua sbagliata farebbe leggere tutte le pagine col modello sbagliato.
        var hint = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: language, DetectedConfidence: 0.99);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be(expected);
    }

    [Fact]
    public void SingleLanguageSet_MatchingTheDocument_SendsThatLanguage()
    {
        var hint = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "en", DetectedConfidence: 0.99);

        UnstructuredLanguageResolver.Resolve(hint, Options(language: "eng")).Should().Be("eng");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.8)] // "supera": a pari merito con la soglia non basta
    public void Detected_NotAboveThreshold_SendsConfiguredDefault(double confidence)
    {
        var hint = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "it", DetectedConfidence: confidence);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be("eng+ita+deu");
    }

    [Fact]
    public void Detected_WithoutConfidence_SendsConfiguredDefault()
    {
        // Prima ingestione: la rilevazione avviene DOPO l'estrazione, e l'entità ha Language = "en"
        // di default con LanguageConfidence = null. Quel "en" non è una rilevazione.
        var hint = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "en", DetectedConfidence: null);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be("eng+ita+deu");
    }

    [Fact]
    public void Unknown_WithZeroConfidence_SendsConfiguredDefault()
    {
        // Testo vuoto (una scansione passata in fast): il rilevatore risponde "unknown" con confidenza 0.
        var hint = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "unknown", DetectedConfidence: 0);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be("eng+ita+deu");
    }

    [Fact]
    public void NoDocument_SendsConfiguredDefault()
    {
        UnstructuredLanguageResolver.Resolve(null, Options()).Should().Be("eng+ita+deu");
    }

    [Theory]
    [InlineData("eng+ita", "eng+ita")]
    [InlineData("eng", "eng")]
    [InlineData(" ENG + ita ,deu ", "eng+ita+deu")]
    [InlineData("en+it", "eng+ita")]
    [InlineData("eng+eng+ita", "eng+ita")]
    public void ConfiguredDefault_IsWhatIsSent_Normalized(string configured, string expected)
    {
        UnstructuredLanguageResolver.Resolve(null, Options(language: configured)).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" + , ")]
    public void BlankConfiguredDefault_FallsBackToBuiltInDefault(string configured)
    {
        UnstructuredLanguageResolver.Resolve(null, Options(language: configured))
            .Should().Be(UnstructuredOptions.DefaultLanguage);
    }

    [Fact]
    public void BuiltInDefaults_AreTheMultilingualSet_AndAMotivatedThreshold()
    {
        var defaults = new UnstructuredOptions();

        defaults.Language.Should().Be("eng+ita+deu");
        defaults.LanguageConfidenceThreshold.Should().Be(0.8);
    }

    [Fact]
    public void DocumentLanguage_OutsideConfiguredSet_SendsConfiguredDefault()
    {
        // fra non è nell'insieme, cioè per contratto il servizio non ha quel modello: chiederlo come
        // primaria non serve, e da solo farebbe fallire l'OCR («couldn't load any languages»). Si manda
        // l'insieme così com'è.
        var overridden = new DocumentLanguageHint(LanguageOverride: "fr", DetectedLanguage: "en", DetectedConfidence: 0.99);
        var detected = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "fr", DetectedConfidence: 0.99);

        UnstructuredLanguageResolver.Resolve(overridden, Options()).Should().Be("eng+ita+deu");
        UnstructuredLanguageResolver.Resolve(detected, Options()).Should().Be("eng+ita+deu");
    }

    [Fact]
    public void DocumentLanguage_InConfiguredSet_ByConfigChange_IsSent()
    {
        var detected = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "fr", DetectedConfidence: 0.99);

        UnstructuredLanguageResolver.Resolve(detected, Options(language: "eng+ita+fra")).Should().Be("fra+eng+ita");
    }

    [Fact]
    public void Threshold_IsConfigurable()
    {
        var hint = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "it", DetectedConfidence: 0.7);

        UnstructuredLanguageResolver.Resolve(hint, Options(threshold: 0.6)).Should().Be("ita+eng+deu");
        UnstructuredLanguageResolver.Resolve(hint, Options(threshold: 0.75)).Should().Be("eng+ita+deu");
    }

    [Theory]
    [InlineData("en", "eng")]
    [InlineData("it", "ita")]
    [InlineData("de", "deu")]
    [InlineData("fr", "fra")]
    [InlineData("es", "spa")]
    [InlineData("pt", "por")]
    [InlineData("pl", "pol")]
    [InlineData("nl", "nld")]
    [InlineData("ja", "jpn")]
    [InlineData("zh", "chi_sim")]
    public void Iso6391_MapsToTesseractCode(string iso6391, string tesseract)
    {
        UnstructuredLanguageResolver.ToTesseractCode(iso6391).Should().Be(tesseract);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("xx")]
    [InlineData("")]
    [InlineData(null)]
    public void UnmappedCode_HasNoTesseractCode(string? code)
    {
        UnstructuredLanguageResolver.ToTesseractCode(code).Should().BeNull();
    }

    /// <summary>
    /// Review di #4515: il caso che la soglia non intercetta. Un regolamento italiano con copertina e
    /// crediti in inglese: <c>LanguageDetector</c> legge solo i primi 2000 caratteri, risponde "en", e
    /// la sua confidenza (quota di lettere latine) vale ~1. L'italiano non deve sparire da ciò che si
    /// manda al servizio. Testo scritto per il test, non tratto da un regolamento (ADR-051/059).
    /// </summary>
    [Fact]
    public void RealDetection_EnglishCoverItalianBody_KeepsItalianInTheSentSet()
    {
        const string englishCover =
            "Lighthouse Keepers. A cooperative game for two to five players. Game design and development " +
            "by the studio team, with illustrations and graphic design by the art department. " +
            "Published under license. All rights reserved. Credits: the authors thank each playtester " +
            "for the many evenings spent at the table, and the families that gave them the time to play. " +
            "Special thanks to the translators and to the proofreaders who checked every card and every " +
            "rule. No part of this product may be reproduced without permission. The game is for ages ten " +
            "and up and plays in about an hour. Visit the website of the publisher to download the latest " +
            "version of the rules, the frequently asked questions and the reference sheet. If a component " +
            "is missing, contact the customer service with the code printed on the back of the box. ";
        const string italianBody =
            "Scopo del gioco. Ogni giocatore guida una squadra di guardiani del faro e cerca di tenere accesa " +
            "la luce per tutta la notte. Il gioco si svolge in una serie di turni. Durante il proprio turno il " +
            "giocatore pesca una carta, la gioca davanti a sé e sposta il segnalino della marea. Una carta " +
            "tempesta obbliga tutti i giocatori a scartare una risorsa. La partita termina quando la marea " +
            "raggiunge la scogliera o quando sono state giocate tutte le carte del mazzo. ";
        var text = string.Concat(Enumerable.Repeat(englishCover, 3)) + string.Concat(Enumerable.Repeat(italianBody, 20));

        var detection = new LanguageDetector().Detect(text);
        detection.DetectedLanguage.Should().Be("en", "the detector only reads the English cover");
        detection.Confidence.Should().BeGreaterThan(0.8, "the confidence counts Latin letters, not the language");

        var sent = UnstructuredLanguageResolver.Resolve(
            new DocumentLanguageHint(LanguageOverride: null, detection.DetectedLanguage, detection.Confidence),
            Options());

        sent.Split('+').Should().Contain("ita", "the Italian body must still be readable by the OCR");
        sent.Split('+')[0].Should().Be("eng", "the detected language goes first, as Tesseract's primary model");
    }
}
