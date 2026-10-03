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
