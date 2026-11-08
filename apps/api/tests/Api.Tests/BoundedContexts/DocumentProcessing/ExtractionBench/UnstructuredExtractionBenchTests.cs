using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Api.Tests.TestHelpers;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// #4434 (a) — il banco di estrazione per <b>Unstructured</b> (<c>fast</c> e <c>hi_res</c>): livello L2/L3
/// della spec <c>2026-10-02-test-dependency-tiers-and-observable-skips.md</c>. Servizio opzionale, quindi
/// salto <c>PREVISTO</c> con il modo di accenderlo; se l'ambiente dichiara il servizio e questo non
/// risponde, <b>fallisce</b> (assenza accertata con una sonda, non dedotta).
/// </summary>
/// <remarks>
/// <para>
/// <b>Stato</b>: queste colonne non sono mai state eseguite in questa PR (nessuno stack Docker è stato
/// acceso). Non esiste ancora una baseline per loro: la crea chi ha il servizio, con
/// <c>EXTRACTION_BENCH_UPDATE_BASELINE=1</c>, e la committa. Finché non c'è, <c>fast</c> fallisce con
/// l'istruzione per crearla (un banco bloccante senza baseline non misurerebbe nulla) e <c>hi_res</c>
/// resta informativo.
/// </para>
/// <para>
/// <c>hi_res</c> è informativo (nessun rosso) finché non se ne misura lo scarto fra più run; <c>fast</c>
/// è deterministico e ha tolleranza zero. Il tempo di <c>hi_res</c> è dell'ordine delle centinaia di
/// secondi a PDF su CPU (stima della ricerca 2026-10-15): non è un test da fast gate.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", "PDF")]
[Trait("TestType", "Bench")]
[Collection(QuestPdfRenderingCollection.Name)]
public sealed class UnstructuredExtractionBenchTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("unstructured-fast", ExtractionStrategy.Fast, true)]
    [InlineData("unstructured-hires", ExtractionStrategy.HiRes, false)]
    public async Task Unstructured_DoesNotFallBelowItsCommittedBaseline(
        string extractorId,
        ExtractionStrategy strategy,
        bool blocking)
    {
        var url = Environment.GetEnvironmentVariable(TestcontainersConfiguration.EnvUnstructuredServiceUrl);
        if (string.IsNullOrWhiteSpace(url))
        {
            Assert.Skip("PREVISTO: needs unstructured-service; start it (e.g. `make dev` in infra/) and set TEST_UNSTRUCTURED_URL=http://localhost:8001; add EXTRACTION_BENCH_UPDATE_BASELINE=1 to create its baseline");
        }

        using var client = new HttpClient
        {
            BaseAddress = new Uri(url!),
            Timeout = TimeSpan.FromMinutes(15), // hi_res: centinaia di secondi a PDF su CPU
        };

        // La sonda: l'ambiente dice che il servizio c'è, quindi un /health che non risponde è un guasto
        // da vedere, non un motivo per saltare.
        using var probe = await client.GetAsync(new Uri("/health", UriKind.Relative), Ct);
        probe.IsSuccessStatusCode.Should().BeTrue(
            $"TEST_UNSTRUCTURED_URL points at {url} but /health answered {(int)probe.StatusCode}");

        var extractor = new UnstructuredPdfTextExtractor(
            new SingleClientFactory(client),
            NullLogger<UnstructuredPdfTextExtractor>.Instance,
            new ExtractionStrategySelector { Current = strategy });

        var report = await ExtractionBenchRunner.RunAsync(
            extractorId,
            ExtractionFixtureSpec.LoadAll(),
            extract: (stream, ct) => extractor.ExtractPagedTextAsync(stream, enableOcrFallback: false, ct),
            observe: ExtractionObservation.FromUnstructured,
            ocrEditTolerance: 0,
            cancellationToken: Ct);

        ExtractionBaselineGate.Enforce(report, tolerance: 0, blocking);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
