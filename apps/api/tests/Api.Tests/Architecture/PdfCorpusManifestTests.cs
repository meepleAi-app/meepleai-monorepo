using System.Text.Json;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4040 — il manifest del corpus PDF punta a file che esistono.
/// </summary>
/// <remarks>
/// <para>
/// Il gate che mancava. `TestData/pdf-corpus/gold-standards.json` dichiarava percorsi con una
/// risalita a conteggio fisso (<c>../../../../data/rulebook/…</c>) che dalla working directory dei
/// test porta a <c>apps/api/tests/</c>, dove <c>data/</c> non esiste. Risultato: i sette siti
/// <c>if (!File.Exists(...)) Assert.Skip("PDF not found: …")</c> della suite di validazione
/// saltavano <b>sempre</b>, in ogni ambiente — e il motivo chiudeva l'indagine, perché «PDF not
/// found» si legge come «manca il file» e non come «il percorso non punta dove credi». I PDF erano
/// committati e c'erano tutti.
/// </para>
/// <para>
/// 🔴 La forma del difetto: un dato **presente** letto come **assente**, con un salto che lo
/// racconta come un ambiente incompleto. È il caso MinIO (#3978) con un'altra causa, e il rimedio è
/// lo stesso: una misura che costa millisecondi invece di una deduzione.
/// </para>
/// <para>
/// È <c>Category=Unit</c> perché deve mordere in <i>Backend Fast</i> su ogni PR, non nella suite
/// opt-in che il manifest serve: quella gira solo con <c>TEST_PDF_SERVICES=true</c>, quindi un
/// percorso rotto là non si manifesta mai come rosso.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4040")]
public sealed class PdfCorpusManifestTests
{
    [Fact]
    public void TheManifestExists()
    {
        // Presupposto dei test sotto: se il manifest sparisse, passerebbero sul vuoto.
        var path = ManifestPath();
        File.Exists(path).Should().BeTrue(
            $"il manifest del corpus e' committato e atteso in {path}");
    }

    [Fact]
    public void EveryManifestEntryPointsToAFileThatExists()
    {
        var entries = LoadEntries();

        entries.Should().NotBeEmpty(
            "un manifest vuoto farebbe passare questo gate senza verificare niente");

        var missing = entries
            .Select(e => new { e.Key, e.RelativePath, Resolved = PdfCorpus.Resolve(e.RelativePath) })
            .Where(e => !File.Exists(e.Resolved))
            .ToList();

        missing.Should().BeEmpty(
            "ogni voce del manifest deve puntare a un PDF committato. Voci che non risolvono:"
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                missing.Select(m => $"  {m.Key}: '{m.RelativePath}' -> {m.Resolved}")));
    }

    [Fact]
    public void ResolutionIgnoresTheLeadingUpwardSegments()
    {
        // La regola che rende il manifest leggibile e il percorso indipendente dalla profondita' di
        // chi lo legge: i segmenti di risalita iniziali esprimono «vai alla radice», non un numero
        // di livelli da rispettare.
        var withUps = PdfCorpus.Resolve("../../../../data/rulebook/azul_rulebook.pdf");
        var withoutUps = PdfCorpus.Resolve("data/rulebook/azul_rulebook.pdf");
        var withMoreUps = PdfCorpus.Resolve("../../data/rulebook/azul_rulebook.pdf");

        withUps.Should().Be(withoutUps);
        withMoreUps.Should().Be(withoutUps);
        withoutUps.Should().StartWith(PdfCorpus.RepositoryRoot);
    }

    [Fact]
    public void ResolutionRejectsAPathMadeOnlyOfUpwardSegments()
    {
        // Un percorso che non nomina un file non deve diventare silenziosamente la radice.
        var act = () => PdfCorpus.Resolve("../../..");
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// #4044 — nessun sorgente di test raggiunge <c>data/</c> con una risalita a conteggio fisso.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Il pattern è ricomparso **quattro volte** in tre file oltre al manifest
    /// (<c>SmolDoclingIntegrationTests</c>, <c>ThreeStagePdfPipelineE2ETests</c>,
    /// <c>UnstructuredPdfExtractionIntegrationTests</c>), e in un caso era sbagliato due volte: la
    /// profondità <b>e</b> il segmento <c>rulebook/</c> mancante. Ogni istanza produceva una guardia
    /// <c>File.Exists</c> che saltava SEMPRE, su PDF committati e presenti.
    /// </para>
    /// <para>
    /// 🔴 La scansione lavora sul testo GREZZO, non su
    /// <see cref="SourceScanner.FindCodeOccurrences"/>: quel metodo salta i letterali, e la cosa da
    /// vietare <b>è</b> un letterale. È lo stesso errore che il gate di #4021 ha commesso e che la
    /// sua contro-prova ha scoperto. Si escludono invece le righe di commento, perché le note che
    /// citano la forma vecchia per spiegarla sono legittime — e ce ne sono.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoTestSourceReachesDataWithAFixedDepthClimb()
    {
        var offenders = new List<string>();
        var testsRoot = Path.Combine(PdfCorpus.RepositoryRoot, "apps", "api", "tests", "Api.Tests");

        foreach (var path in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            // Questo file è l'unica eccezione, e deve esserlo: i test della normalizzazione
            // (`ResolutionIgnoresTheLeadingUpwardSegments`) e la contro-prova qui sotto devono poter
            // NOMINARE la forma vietata — altrimenti non si può né provare che viene normalizzata né
            // che la scansione la riconosce. Il gate l'ha scoperto da sé fallendo su se stesso.
            if (string.Equals(
                    Path.GetFileName(path),
                    "PdfCorpusManifestTests.cs",
                    StringComparison.Ordinal))
            {
                continue;
            }

            var relative = Path.GetRelativePath(testsRoot, path).Replace('\\', '/');
            var lineNumber = 0;

            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("*", StringComparison.Ordinal))
                {
                    continue; // una nota che cita la forma vecchia per spiegarla
                }

                if (line.Contains("\"../../", StringComparison.Ordinal)
                    && line.Contains("/data/", StringComparison.Ordinal))
                {
                    offenders.Add($"  {relative}:{lineNumber}: {trimmed}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "un percorso verso data/ con una risalita a conteggio fisso dipende dalla profondità "
            + "della working directory, che per i test è bin/Debug/net9.0 — quattro livelli su "
            + "portano a apps/api/tests/, che non contiene data/. Usa "
            + $"{nameof(PdfCorpus)}.{nameof(PdfCorpus.Resolve)}(\"data/...\"), che risale a .git. "
            + "Siti non conformi:" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void TheFixedDepthScanWouldSeeTheOldForm()
    {
        // Contro-prova: se la condizione di ricerca smettesse di combaciare — un'altra forma di
        // quoting, un separatore diverso — il test sopra passerebbe sul vuoto. Qui si verifica che
        // la stessa condizione riconosca la stringa che esisteva davvero nel repo.
        const string oldForm = "    private const string P = \"../../../../data/rulebook/x.pdf\";";

        var matches = oldForm.Contains("\"../../", StringComparison.Ordinal)
            && oldForm.Contains("/data/", StringComparison.Ordinal);

        matches.Should().BeTrue(
            "la condizione del gate deve riconoscere la forma che #4044 ha rimosso, altrimenti "
            + "quel gate non sorveglia niente");
    }

    private static string ManifestPath()
    {
        var local = PdfCorpus.ManifestPath;
        if (File.Exists(local))
        {
            return local;
        }

        // Quando il test gira con una working directory diversa dall'output (es. da IDE), il
        // manifest sta comunque accanto all'assembly.
        return Path.Combine(AppContext.BaseDirectory, PdfCorpus.ManifestPath);
    }

    private static List<(string Key, string RelativePath)> LoadEntries()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ManifestPath()));
        var entries = new List<(string, string)>();

        foreach (var tier in document.RootElement.EnumerateObject())
        {
            if (tier.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var entry in tier.Value.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var relative = entry.Value.TryGetProperty("relativePath", out var p)
                    ? p.GetString()
                    : entry.Value.TryGetProperty("RelativePath", out var p2) ? p2.GetString() : null;

                if (!string.IsNullOrWhiteSpace(relative))
                {
                    entries.Add((entry.Name, relative));
                }
            }
        }

        return entries;
    }
}
