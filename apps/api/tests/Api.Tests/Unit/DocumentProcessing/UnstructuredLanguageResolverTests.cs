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
/// Una lingua del documento che non sta in quell'insieme ricade sull'insieme intero: l'insieme dichiara
/// le lingue che il servizio sa leggere, e Tesseract con una sola lingua non installata non legge nulla.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4435")]
public sealed class UnstructuredLanguageResolverTests
{
    private static UnstructuredOptions Options(string language = "eng+ita+deu", double threshold = 0.8) =>
        new() { Language = language, LanguageConfidenceThreshold = threshold };

    [Fact]
    public void Override_De_SendsDeu_EvenWhenDetectionSaysEnglish()
    {
        var hint = new DocumentLanguageHint(LanguageOverride: "de", DetectedLanguage: "en", DetectedConfidence: 0.99);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be("deu");
    }

    [Fact]
    public void Override_IsCaseAndRegionInsensitive()
    {
        var hint = new DocumentLanguageHint(LanguageOverride: " DE-at ", DetectedLanguage: null, DetectedConfidence: null);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be("deu");
    }

    [Fact]
    public void Detected_AboveThreshold_SendsDetectedLanguage()
    {
        var hint = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "en", DetectedConfidence: 0.95);

        UnstructuredLanguageResolver.Resolve(hint, Options()).Should().Be("eng");
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
        // fra non è nell'insieme: inviarlo da solo a un servizio senza il pacchetto fra farebbe fallire
        // l'OCR (Tesseract «couldn't load any languages»). Meglio l'insieme intero.
        var overridden = new DocumentLanguageHint(LanguageOverride: "fr", DetectedLanguage: "en", DetectedConfidence: 0.99);
        var detected = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "fr", DetectedConfidence: 0.99);

        UnstructuredLanguageResolver.Resolve(overridden, Options()).Should().Be("eng+ita+deu");
        UnstructuredLanguageResolver.Resolve(detected, Options()).Should().Be("eng+ita+deu");
    }

    [Fact]
    public void DocumentLanguage_InConfiguredSet_ByConfigChange_IsSent()
    {
        var detected = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "fr", DetectedConfidence: 0.99);

        UnstructuredLanguageResolver.Resolve(detected, Options(language: "eng+ita+fra")).Should().Be("fra");
    }

    [Fact]
    public void Threshold_IsConfigurable()
    {
        var hint = new DocumentLanguageHint(LanguageOverride: null, DetectedLanguage: "it", DetectedConfidence: 0.7);

        UnstructuredLanguageResolver.Resolve(hint, Options(threshold: 0.6)).Should().Be("ita");
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
}
