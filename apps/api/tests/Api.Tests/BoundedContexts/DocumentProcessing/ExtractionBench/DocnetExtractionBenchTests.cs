using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// #4434 (a) — il banco di estrazione per <b>Docnet</b>: l'estrattore effettivo (decisioni dell'epic
/// #4433, 2026-11-05) e l'unico che gira in-process, senza servizi.
/// </summary>
/// <remarks>
/// <para>
/// È <c>Category=Unit</c> di proposito: il fast gate (deny-list di <c>dev-fast.yml</c>) lo esegue a ogni PR.
/// Non c'è un servizio da accendere, quindi nulla da saltare: se questo banco non gira, è un difetto.
/// Docnet su Linux usa il runtime già dichiarato in <c>Api.Tests.csproj</c> (<c>DocnetRuntime=linux</c>).
/// </para>
/// <para>
/// <b>Cosa si aspetta da Docnet</b> (baseline in <c>TestData/extraction-bench/baseline.docnet.json</c>):
/// testo e ordine di lettura sì; titoli e celle strutturate <i>non applicabili</i> (Docnet non classifica
/// elementi); <b>nessun testo</b> sulla pagina scansionata, perché Docnet non fa OCR (#4437). Quell'ultimo
/// zero è un risultato vero e sta nella baseline: il banco deve accorgersi se cambia.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", "PDF")]
public sealed class DocnetExtractionBenchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static DocnetPdfTextExtractor CreateExtractor()
    {
        return new DocnetPdfTextExtractor(NullLogger<DocnetPdfTextExtractor>.Instance);
    }

    internal static Task<ExtractionBenchReport> RunAsync(
        Func<ExtractionFixtureSpec, ExtractionExpected>? expectedFor = null)
    {
        var extractor = CreateExtractor();
        return ExtractionBenchRunner.RunAsync(
            extractorId: "docnet",
            fixtures: ExtractionFixtureSpec.LoadAll(),
            extract: (stream, ct) => extractor.ExtractPagedTextAsync(stream, enableOcrFallback: false, ct),
            observe: ExtractionObservation.FromDocnet,
            ocrEditTolerance: 0,
            cancellationToken: Ct,
            expectedFor: expectedFor);
    }

    [Fact]
    public async Task Docnet_DoesNotFallBelowItsCommittedBaseline()
    {
        var report = await RunAsync();

        // Tolleranza zero: Docnet è deterministico a parità di versione. Un cambio di versione che sposta
        // un numero aggiorna la baseline nella stessa PR, col diff visibile.
        ExtractionBaselineGate.Enforce(report, tolerance: 0, blocking: true);

        report.Fixtures.Should().NotBeEmpty("a bench that measured no fixture proves nothing");
    }

    [Fact]
    public async Task Docnet_FindsNoTextOnTheScannedPage_ButFindsItOnThePageBefore()
    {
        // È il risultato atteso di Docnet su una scansione (#4437), non un errore del banco: lo verifico
        // direttamente, oltre che attraverso la baseline, così un guasto del renderer (pagina 2 che
        // contiene testo per sbaglio) non si scambia per «Docnet ora legge le scansioni».
        var spec = ExtractionFixtureSpec.LoadAll().Single(f => f.Id == "scanned-page-en");
        using var stream = new MemoryStream(ExtractionFixtureRenderer.RenderPdf(spec));

        var result = await CreateExtractor().ExtractPagedTextAsync(stream, enableOcrFallback: false, Ct);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.PageChunks.Should().HaveCount(2);
        result.PageChunks.Single(p => p.PageNumber == 1).IsEmpty.Should().BeFalse();
        result.PageChunks.Single(p => p.PageNumber == 2).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task TheBench_SeesAWrongExpectation()
    {
        // La perturbazione dell'atteso come test permanente: un atteso che il PDF non soddisfa deve far
        // scendere le metriche che si applicano a Docnet. Senza questo, un banco che restituisse sempre
        // 1.0 passerebbe tutti gli altri test.
        var honest = await RunAsync();
        var sabotaged = await RunAsync(spec =>
        {
            var expected = ExtractionExpected.From(spec);
            return expected with
            {
                Paragraphs = [.. expected.Paragraphs, "A sentence that this document never contains."],
                TableCells = [.. expected.TableCells, "a cell that is not there"],
                // Invertito, e con un titolo che il documento non contiene: l'inversione muove readingOrder,
                // le parole inesistenti muovono wordRecall (le stesse parole in ordine diverso non lo muoverebbero).
                ReadingOrder = [.. expected.ReadingOrder.AsEnumerable().Reverse(), "Ghost heading nobody ever printed"],
            };
        });

        // Le sole metriche che un atteso sbagliato può muovere per Docnet. scanWordRecall e il tableCellRecall
        // di graphic-table-de valgono già 0 in baseline e non possono scendere (il filtro «> 0» sotto le salta);
        // headingRecall e tableCellF1 sono non applicabili.
        var applicable = new[]
        {
            ExtractionScoring.ParagraphRecall,
            ExtractionScoring.ReadingOrder,
            ExtractionScoring.WordRecall,
            ExtractionScoring.TableCellRecall,
        };

        // Solo dove il valore onesto è sopra zero: da zero non si può scendere, e un confronto lì
        // direbbe soltanto che il metro non ha più nulla da perdere.
        var compared = 0;
        foreach (var metric in applicable)
        {
            foreach (var (id, honestResult) in honest.Fixtures.Where(f => f.Value.Metrics[metric] is > 0))
            {
                sabotaged.Fixtures[id].Metrics[metric].Should().BeLessThan(
                    honestResult.Metrics[metric]!.Value,
                    $"'{id}'/{metric} must drop when the expectation is wrong");
                compared++;
            }
        }

        compared.Should().BeGreaterThan(0, "a perturbation that compared nothing proves nothing");

        // …e la baseline lo trasforma in un rosso.
        var failures = ExtractionBaseline.FromReport(honest).Regressions(sabotaged, tolerance: 0);
        failures.Should().NotBeEmpty();
    }
}
