using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #4931 — misura, non gate: per ogni PDF di una cartella, quante pagine stanno sotto <see cref="NearlyEmptyPages.MaxCharacters"/>
/// con l'estrattore vero (Docnet) e quanto «inchiostro» c'è in quelle pagine, così una pagina bianca si distingue da una
/// pagina solo-figura o da una scansione. La regola di <see cref="NearlyEmptyPages"/> si sceglie su questi numeri.
/// </summary>
/// <remarks>
/// <para>
/// È <c>Unit</c> di proposito, come il banco di Unstructured è <c>Integration</c>: un test solo «Manual» non lo raggiunge
/// nessun gate (<c>TestCategoryGateArchitectureTests</c>), quindi nessuno lo vedrebbe rompersi. Senza la cartella salta con un
/// motivo <c>PREVISTO:</c>; con la cartella misura.
/// </para>
/// <para>
/// Comando (Git Bash, da <c>apps/api</c>; il corpus locale è <c>data/rulebook</c>, non versionato):
/// <c>NEARLY_EMPTY_CORPUS_DIR=../../data/rulebook dotnet test tests/Api.Tests/Api.Tests.csproj -c Release --filter
/// "FullyQualifiedName~NearlyEmptyPagesCorpusMeasurement"</c>. Scrive <c>nearly-empty-pages.json</c> in
/// <c>NEARLY_EMPTY_OUTPUT_DIR</c> (default: la cartella temporanea) e un riepilogo nell'output del test.
/// </para>
/// <para>
/// Inchiostro = frazione di pixel con luminosità media sotto <see cref="InkLuminanceBelow"/> in un render a mezza
/// scala. Una pagina con inchiostro sotto <see cref="BlankInkBelow"/> è «bianca». Le soglie sono nel JSON
/// insieme ai valori grezzi per pagina, quindi si possono rivedere senza rieseguire la misura.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4931")]
public sealed class NearlyEmptyPagesCorpusMeasurement
{
    private const string CorpusEnv = "NEARLY_EMPTY_CORPUS_DIR";
    private const string OutputEnv = "NEARLY_EMPTY_OUTPUT_DIR";
    private const double RenderScale = 0.5d;
    private const int InkLuminanceBelow = 200;
    private const double BlankInkBelow = 0.01d;

    private readonly ITestOutputHelper _output;

    public NearlyEmptyPagesCorpusMeasurement(ITestOutputHelper output) => _output = output;

    [Fact(Timeout = 30 * 60 * 1000)]
    public async Task MeasureThePerPageDistribution()
    {
        var corpus = Environment.GetEnvironmentVariable(CorpusEnv);
        if (string.IsNullOrWhiteSpace(corpus))
        {
            Assert.Skip("PREVISTO: misura manuale (#4931); imposta NEARLY_EMPTY_CORPUS_DIR alla cartella dei PDF (in locale: data/rulebook) ed eventualmente NEARLY_EMPTY_OUTPUT_DIR; il risultato e' nearly-empty-pages.json");
        }

        Assert.True(Directory.Exists(corpus), $"{CorpusEnv} punta a una cartella che non esiste: {corpus}");

        var extractor = new DocnetPdfTextExtractor(NullLogger<DocnetPdfTextExtractor>.Instance);
        var documents = new List<DocumentMeasure>();
        foreach (var path in Directory.EnumerateFiles(corpus!, "*.pdf", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            documents.Add(await MeasureAsync(extractor, path, TestContext.Current.CancellationToken));
        }

        var outputDir = Environment.GetEnvironmentVariable(OutputEnv);
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            outputDir = Path.Combine(Path.GetTempPath(), "nearly-empty-pages");
        }

        Directory.CreateDirectory(outputDir);
        var file = Path.Combine(outputDir, "nearly-empty-pages.json");
        var report = new
        {
            capturedAt = DateTime.UtcNow,
            maxCharacters = NearlyEmptyPages.MaxCharacters,
            renderScale = RenderScale,
            inkLuminanceBelow = InkLuminanceBelow,
            blankInkBelow = BlankInkBelow,
            documents,
        };
        await File.WriteAllTextAsync(
            file,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            TestContext.Current.CancellationToken);

        _output.WriteLine($"Scritto {file}");
        foreach (var d in documents.Where(d => d.Error is null).OrderByDescending(d => d.FractionNearlyEmpty).Take(25))
        {
            _output.WriteLine(
                $"{d.FractionNearlyEmpty,5:P0} {d.NearlyEmpty,3}/{d.Pages,3} (bianche {d.NearlyEmptyBlank}, con inchiostro {d.NearlyEmptyInked}, " +
                $"prima {(d.FirstIsNearlyEmpty ? "sì" : "no")}, ultima {(d.LastIsNearlyEmpty ? "sì" : "no")}, serie più lunga {d.LongestRun})  {d.File}");
        }

        Assert.NotEmpty(documents);
    }

    private static async Task<DocumentMeasure> MeasureAsync(DocnetPdfTextExtractor extractor, string path, CancellationToken ct)
    {
        var name = Path.GetFileName(path);
        try
        {
            await using var stream = File.OpenRead(path);
            var result = await extractor.ExtractPagedTextAsync(stream, enableOcrFallback: false, ct);
            if (!result.Success)
            {
                return DocumentMeasure.Failed(name, result.ErrorMessage ?? "extraction failed");
            }

            var chars = result.PageChunks.OrderBy(p => p.PageNumber).Select(p => (p.Text ?? string.Empty).Trim().Length).ToArray();
            var ink = MeasureInk(path, chars.Length);
            return DocumentMeasure.From(name, chars, ink, result.PageChunks.Any(p => p.PageIsEstimated), result.TextLayerOnly);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return DocumentMeasure.Failed(name, ex.GetType().Name + ": " + ex.Message);
        }
    }

    /// <summary>Frazione di pixel «scuri» di ogni pagina. Mai <c>using</c> su <c>DocLib.Instance</c> (#4929).</summary>
    private static double[] MeasureInk(string path, int expectedPages)
    {
        var library = DocLib.Instance;
        using var reader = library.GetDocReader(path, new PageDimensions(RenderScale));
        var pages = Math.Min(reader.GetPageCount(), expectedPages);
        var ink = new double[expectedPages];
        for (var i = 0; i < pages; i++)
        {
            using var page = reader.GetPageReader(i);
            var bgra = page.GetImage();
            var pixels = bgra.Length / 4;
            if (pixels == 0)
            {
                continue;
            }

            var dark = 0;
            for (var p = 0; p < bgra.Length; p += 4)
            {
                if ((bgra[p] + bgra[p + 1] + bgra[p + 2]) / 3 < InkLuminanceBelow)
                {
                    dark++;
                }
            }

            ink[i] = (double)dark / pixels;
        }

        return ink;
    }

    private sealed record DocumentMeasure(
        string File,
        string? Error,
        int Pages,
        bool TextLayerOnly,
        bool PageIsEstimated,
        int NearlyEmpty,
        int NearlyEmptyBlank,
        int NearlyEmptyInked,
        double FractionNearlyEmpty,
        bool FirstIsNearlyEmpty,
        bool LastIsNearlyEmpty,
        int LongestRun,
        int[] Chars,
        double[] Ink)
    {
        public static DocumentMeasure Failed(string file, string error) =>
            new(file, error, 0, false, false, 0, 0, 0, 0d, false, false, 0, [], []);

        public static DocumentMeasure From(string file, int[] chars, double[] ink, bool estimated, bool textLayerOnly)
        {
            var empty = chars.Select(c => c < NearlyEmptyPages.MaxCharacters).ToArray();
            var run = 0;
            var longest = 0;
            foreach (var e in empty)
            {
                run = e ? run + 1 : 0;
                longest = Math.Max(longest, run);
            }

            var blank = Enumerable.Range(0, chars.Length).Count(i => empty[i] && ink[i] < BlankInkBelow);
            var count = empty.Count(e => e);
            return new DocumentMeasure(
                file,
                null,
                chars.Length,
                textLayerOnly,
                estimated,
                count,
                blank,
                count - blank,
                chars.Length == 0 ? 0d : (double)count / chars.Length,
                chars.Length > 0 && empty[0],
                chars.Length > 0 && empty[^1],
                longest,
                chars,
                ink);
        }
    }
}
