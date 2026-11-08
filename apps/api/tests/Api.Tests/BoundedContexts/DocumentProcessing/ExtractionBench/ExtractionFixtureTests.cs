using System.Text;
using Api.Tests.Constants;
using FluentAssertions;
using SkiaSharp;
using Xunit;
using Api.Tests.TestHelpers;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// #4434 (a) — le fixture sono ciò che dicono di essere. Il banco misura gli estrattori; questi test
/// misurano il banco: se il renderer producesse una «scansione» con un livello di testo, o una pagina
/// bianca, Docnet risulterebbe «migliorato» o «peggiorato» per un difetto della fixture.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", "PDF")]
[Collection(QuestPdfRenderingCollection.Name)]
public sealed class ExtractionFixtureTests
{
    /// <summary>Sotto questa soglia di pixel scuri una pagina rasterizzata è, a tutti gli effetti, vuota.</summary>
    private const int MinInkPixels = 500;

    private static ExtractionFixtureSpec Fixture(string id) =>
        ExtractionFixtureSpec.LoadAll().Single(f => f.Id == id);

    [Fact]
    public void Fixtures_AreLoadedFromTestData_WithUniqueIds()
    {
        var all = ExtractionFixtureSpec.LoadAll();

        all.Should().NotBeEmpty("TestData/extraction-bench/fixtures/*.json must reach the test output");
        all.Select(f => f.Id).Should().OnlyHaveUniqueItems();
        all.Select(f => f.Language).Distinct().Should().BeEquivalentTo(["en", "it", "de"]);
    }

    [Fact]
    public void EveryFixture_RendersAValidPdf()
    {
        foreach (var spec in ExtractionFixtureSpec.LoadAll())
        {
            var pdf = ExtractionFixtureRenderer.RenderPdf(spec);

            Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-", $"fixture '{spec.Id}'");
            pdf.Length.Should().BeGreaterThan(1024, $"fixture '{spec.Id}' must not be an empty document");
        }
    }

    [Fact]
    public void EveryFixture_HasExpectationsToMeasure()
    {
        foreach (var spec in ExtractionFixtureSpec.LoadAll())
        {
            var expected = ExtractionExpected.From(spec);
            (expected.Paragraphs.Count + expected.TableCells.Count + expected.ScanWords.Count)
                .Should().BeGreaterThan(0, $"fixture '{spec.Id}' would measure nothing");
        }
    }

    [Fact]
    public void TableCells_NeverAppearInTheRestOfTheFixtureText()
    {
        // Una cella che compare anche in un paragrafo viene «trovata» da qualunque estrattore che legga il
        // paragrafo: tableCellRecall misurerebbe il paragrafo, non la tabella. È il difetto che la prima
        // stesura di graphic-table-de aveva — l'intestazione «Kosten» stava nella frase sopra la tabella,
        // e Docnet, che non legge la tabella disegnata, risultava a 0,17 invece che a 0.
        // Il confronto è quello delle metriche (senza spazi, sottostringa), non per parola.
        foreach (var spec in ExtractionFixtureSpec.LoadAll())
        {
            var otherText = string.Concat(
                spec.Pages
                    .SelectMany(p => p.Blocks)
                    .Where(b => b.Type != FixtureBlockSpec.TypeTable)
                    .Select(b => ExtractionTextMatcher.Squeeze(b.Text)));

            var leaked = spec.Pages
                .SelectMany(p => p.Blocks)
                .Where(b => b.Type == FixtureBlockSpec.TypeTable)
                .SelectMany(b => b.Rows!.SelectMany(r => r))
                .Where(cell => ExtractionTextMatcher.ContainsPhrase(otherText, ExtractionTextMatcher.Squeeze(cell)))
                .ToList();

            leaked.Should().BeEmpty($"fixture '{spec.Id}': a table cell found in its own text measures the text, not the table");
        }
    }

    [Fact]
    public void ScanPage_EmbedsARasterWithInk()
    {
        // L'incognita del brief: QuestPDF sa produrre una pagina «solo immagine»? Sì, rasterizzando
        // con GenerateImages. Due prove distinte: il PNG ha pixel scuri, e il PDF finale incorpora
        // un'immagine (XObject) che nelle fixture di solo testo non c'è.
        var spec = Fixture("scanned-page-en");
        var scanPage = spec.Pages.Single(p => p.IsScan);

        DarkPixels(ExtractionFixtureRenderer.RasterizePage(scanPage)).Should().BeGreaterThan(MinInkPixels);
        ImageCount(ExtractionFixtureRenderer.RenderPdf(spec)).Should().Be(1);
    }

    [Fact]
    public void ImageTable_EmbedsARasterWithInk()
    {
        var spec = Fixture("graphic-table-de");
        var table = spec.Pages.Single().Blocks.Single(b => b.IsImageTable);

        DarkPixels(ExtractionFixtureRenderer.RasterizeTable(table)).Should().BeGreaterThan(MinInkPixels);
        ImageCount(ExtractionFixtureRenderer.RenderPdf(spec)).Should().Be(1);
    }

    [Theory]
    [InlineData("two-columns-en")]
    [InlineData("text-table-it")]
    [InlineData("inline-icons-en")]
    public void TextFixtures_EmbedNoRaster(string id)
    {
        ImageCount(ExtractionFixtureRenderer.RenderPdf(Fixture(id))).Should().Be(0);
    }

    private static int DarkPixels(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        bitmap.Should().NotBeNull("the rasterized page must be a decodable PNG");
        var dark = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                if (c.Alpha > 0 && (c.Red + c.Green + c.Blue) / 3 < 160)
                {
                    dark++;
                }
            }
        }

        return dark;
    }

    /// <summary>
    /// Le immagini incorporate nel PDF, contate dai dizionari degli oggetti (restano in chiaro anche con gli
    /// stream compressi). QuestPDF scrive per ogni PNG con trasparenza <b>due</b> XObject
    /// <c>/Subtype /Image</c>: l'immagine e la sua maschera morbida, che l'immagine cita con
    /// <c>/SMask N 0 R</c>. Le maschere non sono immagini a sé, quindi si sottraggono.
    /// </summary>
    private static int ImageCount(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        return Occurrences(text, "/Subtype /Image") - Occurrences(text, "/SMask ");
    }

    private static int Occurrences(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(needle, i + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
