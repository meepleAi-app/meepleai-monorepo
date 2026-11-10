using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// Il punto in cui il banco diventa rosso o verde (#4434 a): confronta un report con la baseline
/// committata, scrive l'artifact, e — solo su richiesta esplicita — riscrive la baseline.
/// </summary>
internal static class ExtractionBaselineGate
{
    /// <summary>Se vale <c>1</c>, il banco riscrive la baseline invece di confrontarla. Mai implicito.</summary>
    public const string UpdateVariable = "EXTRACTION_BENCH_UPDATE_BASELINE";

    public static void Enforce(ExtractionBenchReport report, double tolerance, bool blocking)
    {
        var artifact = report.WriteArtifact();
        Console.WriteLine($"[extraction-bench] {report.Extractor}: metrics written to {artifact}");

        if (string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail(
                    $"{UpdateVariable}=1 is refused on CI: a baseline is rewritten by a person, who then commits the diff " +
                    "and explains it in the PR.");
            }

            var target = SourcePath(report.Extractor);
            File.WriteAllText(target, ExtractionBaseline.FromReport(report).ToJson() + Environment.NewLine);
            Console.WriteLine($"[extraction-bench] baseline REWRITTEN: {target} — review the diff before committing.");
            return;
        }

        var path = BuildOutputPath(report.Extractor);
        if (!File.Exists(path))
        {
            var missing =
                $"No baseline for '{report.Extractor}' ({path}). Create it on purpose with {UpdateVariable}=1 " +
                "(docs/for-developers/testing/extraction-bench.md), then commit it.";
            if (blocking)
            {
                Assert.Fail(missing);
            }

            Console.WriteLine($"[extraction-bench] informational (not blocking): {missing}");
            return;
        }

        var failures = ExtractionBaseline.FromJson(File.ReadAllText(path)).Regressions(report, tolerance);
        if (failures.Count == 0)
        {
            return;
        }

        var message = $"{report.Extractor} fell below its baseline:\n  - " + string.Join("\n  - ", failures);
        if (blocking)
        {
            Assert.Fail(message);
        }

        Console.WriteLine($"[extraction-bench] informational (not blocking): {message}");
    }

    /// <summary>La copia della baseline che i binari di test leggono (copiata da <c>TestData</c>).</summary>
    private static string BuildOutputPath(string extractor) =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "extraction-bench", $"baseline.{extractor}.json");

    /// <summary>Il file nel sorgente, quello che si committa: si risale fino alla cartella di <c>Api.Tests.csproj</c>.</summary>
    private static string SourcePath(string extractor)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Api.Tests.csproj")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Api.Tests.csproj not found above the test binaries: cannot locate the source baseline.");
        }

        var folder = Path.Combine(dir.FullName, "TestData", "extraction-bench");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, $"baseline.{extractor}.json");
    }
}
