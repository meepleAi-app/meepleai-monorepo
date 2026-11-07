#pragma warning disable MA0048 // File name must match type name — the report and its per-fixture record travel together
using System.Text.Json;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>L'esito di una fixture: le metriche, e quanto ha richiesto (solo informativo).</summary>
/// <param name="Metrics">Nome della metrica → valore; <c>null</c> = non applicabile.</param>
/// <param name="Pages">Le pagine del documento.</param>
/// <param name="Seconds">Il tempo dell'estrazione. Va nell'artifact; non si asserisce mai.</param>
internal sealed record FixtureResult(IReadOnlyDictionary<string, double?> Metrics, int Pages, double Seconds)
{
    public double SecondsPerPage => Pages > 0 ? Seconds / Pages : 0;
}

/// <summary>L'esito del banco per un estrattore: è ciò che diventa artifact e, rigenerata, baseline.</summary>
/// <param name="Extractor">Identificativo dell'estrattore (<c>docnet</c>, <c>unstructured-fast</c>, <c>unstructured-hires</c>).</param>
/// <param name="Fixtures">Id della fixture → esito.</param>
internal sealed record ExtractionBenchReport(string Extractor, IReadOnlyDictionary<string, FixtureResult> Fixtures)
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public string ToJson() => JsonSerializer.Serialize(
        new
        {
            extractor = Extractor,
            fixtures = Fixtures
                .OrderBy(f => f.Key, StringComparer.Ordinal)
                .ToDictionary(
                    f => f.Key,
                    f => new
                    {
                        metrics = f.Value.Metrics,
                        pages = f.Value.Pages,
                        seconds = Math.Round(f.Value.Seconds, 3),
                        secondsPerPage = Math.Round(f.Value.SecondsPerPage, 3),
                    }),
        },
        JsonOptions);

    /// <summary>
    /// Scrive l'artifact nella cartella <c>EXTRACTION_BENCH_OUTPUT_DIR</c> (la CI la imposta e la carica);
    /// in locale, accanto ai binari di test. Restituisce il percorso scritto.
    /// </summary>
    public string WriteArtifact()
    {
        var dir = Environment.GetEnvironmentVariable(OutputDirVariable);
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, "extraction-bench-results");
        }

        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"extraction-bench-{Extractor}.json");
        File.WriteAllText(path, ToJson());
        return path;
    }

    public const string OutputDirVariable = "EXTRACTION_BENCH_OUTPUT_DIR";
}
