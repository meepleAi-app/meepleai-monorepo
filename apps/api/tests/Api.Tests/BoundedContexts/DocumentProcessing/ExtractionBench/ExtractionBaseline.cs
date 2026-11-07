using System.Text.Json;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// La baseline committata di un estrattore (#4434 a): per ogni fixture e metrica, il valore sotto cui il
/// banco non deve scendere. Modello: <c>infra/fixtures/rag-golden-baseline.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>«Non peggiore» = metrica ≥ baseline.</b> La tolleranza non sta nel file: la fissa chi chiama
/// (<c>0</c> per Docnet e Unstructured <c>fast</c>, che sono deterministici a parità di versione;
/// <c>hi_res</c> è solo informativo finché non se ne misura lo scarto fra più run).
/// </para>
/// <para>
/// Il confronto fallisce anche per ciò che <i>manca</i>: una fixture o una metrica assente dalla baseline
/// (sarebbero non sorvegliate), una fixture della baseline che il run non ha misurato, una metrica che
/// era misurabile e non lo è più. Una metrica che <i>diventa</i> applicabile non è un fallimento.
/// </para>
/// <para>
/// <b>Rigenerarla</b> è un atto esplicito: <c>EXTRACTION_BENCH_UPDATE_BASELINE=1</c> (vedi
/// <c>docs/for-developers/testing/extraction-bench.md</c>); la CI lo rifiuta.
/// </para>
/// </remarks>
/// <param name="Extractor">Identificativo dell'estrattore.</param>
/// <param name="Fixtures">Id della fixture → (metrica → valore; <c>null</c> = non applicabile).</param>
internal sealed record ExtractionBaseline(
    string Extractor,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, double?>> Fixtures)
{
    private const double Epsilon = 1e-9;

    public static ExtractionBaseline FromReport(ExtractionBenchReport report) =>
        new(
            report.Extractor,
            report.Fixtures
                .OrderBy(f => f.Key, StringComparer.Ordinal)
                .ToDictionary(
                    f => f.Key,
                    f => (IReadOnlyDictionary<string, double?>)new SortedDictionary<string, double?>(
                        f.Value.Metrics.ToDictionary(m => m.Key, m => m.Value, StringComparer.Ordinal),
                        StringComparer.Ordinal),
                    StringComparer.Ordinal));

    public string ToJson() => JsonSerializer.Serialize(
        new
        {
            extractor = Extractor,
            fixtures = Fixtures.OrderBy(f => f.Key, StringComparer.Ordinal).ToDictionary(f => f.Key, f => f.Value),
        },
        ExtractionBenchReport.JsonOptions);

    public static ExtractionBaseline FromJson(string json)
    {
        var raw = JsonSerializer.Deserialize<RawBaseline>(json, ExtractionBenchReport.JsonOptions)
                  ?? throw new InvalidOperationException("Baseline JSON is empty");
        return new ExtractionBaseline(
            raw.Extractor,
            raw.Fixtures.ToDictionary(
                f => f.Key,
                f => (IReadOnlyDictionary<string, double?>)f.Value,
                StringComparer.Ordinal));
    }

    /// <summary>I fallimenti del confronto, in chiaro; vuoto = il run non scende sotto la baseline.</summary>
    public IReadOnlyList<string> Regressions(ExtractionBenchReport report, double tolerance)
    {
        var failures = new List<string>();

        foreach (var (fixtureId, result) in report.Fixtures.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            if (!Fixtures.TryGetValue(fixtureId, out var expectedMetrics))
            {
                failures.Add($"fixture '{fixtureId}' is not in the baseline (unguarded): regenerate the baseline");
                continue;
            }

            foreach (var (metric, actual) in result.Metrics.OrderBy(m => m.Key, StringComparer.Ordinal))
            {
                if (!expectedMetrics.TryGetValue(metric, out var floor))
                {
                    failures.Add($"'{fixtureId}'/{metric} is not in the baseline (unguarded): regenerate the baseline");
                }
                else if (floor is not null && actual is null)
                {
                    failures.Add($"'{fixtureId}'/{metric} was {floor:0.####} and is no longer measurable");
                }
                else if (floor is not null && actual < floor - tolerance - Epsilon)
                {
                    failures.Add($"'{fixtureId}'/{metric} fell from {floor:0.####} to {actual:0.####} (tolerance {tolerance})");
                }
            }
        }

        // Una metrica in baseline che il run non produce (rinominata, tolta dal punteggio): il suo pavimento
        // resterebbe scritto senza che nessuno lo controlli più.
        foreach (var (fixtureId, result) in report.Fixtures.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            if (Fixtures.TryGetValue(fixtureId, out var expectedMetrics))
            {
                failures.AddRange(expectedMetrics.Keys
                    .Where(metric => !result.Metrics.ContainsKey(metric))
                    .OrderBy(metric => metric, StringComparer.Ordinal)
                    .Select(metric => $"'{fixtureId}'/{metric} is in the baseline but the run did not produce it"));
            }
        }

        failures.AddRange(Fixtures.Keys
            .Where(id => !report.Fixtures.ContainsKey(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => $"fixture '{id}' is in the baseline but the run did not measure it"));

        return failures;
    }

    private sealed record RawBaseline(string Extractor, Dictionary<string, Dictionary<string, double?>> Fixtures);
}
