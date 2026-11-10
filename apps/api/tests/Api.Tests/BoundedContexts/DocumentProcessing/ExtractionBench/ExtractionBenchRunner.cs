using System.Diagnostics;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// Esegue un estrattore su tutte le fixture e ne produce il <see cref="ExtractionBenchReport"/> (#4434 a).
/// Il PDF si genera <b>fuori</b> dal cronometro: i secondi sono quelli dell'estrattore, non del renderer.
/// </summary>
internal static class ExtractionBenchRunner
{
    public static async Task<ExtractionBenchReport> RunAsync(
        string extractorId,
        IReadOnlyList<ExtractionFixtureSpec> fixtures,
        Func<Stream, CancellationToken, Task<PagedTextExtractionResult>> extract,
        Func<PagedTextExtractionResult, ExtractionObservation> observe,
        double ocrEditTolerance,
        CancellationToken cancellationToken,
        Func<ExtractionFixtureSpec, ExtractionExpected>? expectedFor = null)
    {
        expectedFor ??= ExtractionExpected.From;
        var results = new Dictionary<string, FixtureResult>(StringComparer.Ordinal);

        foreach (var spec in fixtures)
        {
            var pdf = ExtractionFixtureRenderer.RenderPdf(spec);
            using var stream = new MemoryStream(pdf);

            var clock = Stopwatch.StartNew();
            var extracted = await extract(stream, cancellationToken).ConfigureAwait(false);
            clock.Stop();

            var observation = observe(extracted);
            results[spec.Id] = new FixtureResult(
                ExtractionScoring.Score(expectedFor(spec), observation, ocrEditTolerance),
                observation.PageCount,
                clock.Elapsed.TotalSeconds);
        }

        return new ExtractionBenchReport(extractorId, results);
    }
}
