using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>
/// #4950 — architecture gate: <b>il prodotto genera PDF con QuestPDF solo attraverso <c>QuestPdfRenderGate</c></b>.
/// <para>
/// Due render QuestPDF sovrapposti nello stesso processo possono dare un PDF con la mappa <c>ToUnicode</c> del font
/// sbagliata: a video è giusto, ma chi lo legge ne ritrova una frazione, o niente (#4933 lo ha misurato sulle fixture di
/// test; #4950 su tutti e tre i renderer di prodotto). Il cancello li mette in fila. Un renderer nuovo che chiamasse
/// <c>GeneratePdf</c> da solo riporterebbe il difetto senza che il suo codice lo dica: è la posizione che sbaglia, non una riga.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class QuestPdfRenderGateArchitectureTests
{
    /// <summary>Le uscite di QuestPDF che girano il motore di layout e di font, cioè quelle che competono.</summary>
    private static readonly string[] Generators =
    [
        "GeneratePdf(",
        "GenerateImages(",
        "GenerateSvg(",
        "GenerateXps(",
    ];

    private const string Gate = "Infrastructure/QuestPdfRenderGate.cs";
    private const string GateType = "QuestPdfRenderGate.";

    /// <summary>
    /// File che generano da soli per una ragione scritta. Ogni voce porta il perché: senza, non è un'esenzione, è il
    /// difetto che questo gate esiste per fermare.
    /// </summary>
    private static readonly Dictionary<string, string> Exempt = new(StringComparer.Ordinal)
    {
        // Vuoto per costruzione. La prima voce qui dentro deve portare un motivo scritto.
    };

    private static readonly string[] KnownRenderers =
    [
        "Services/PdfExportFormatter.cs",
        "BoundedContexts/Administration/Infrastructure/Services/Formatters/PdfReportFormatter.cs",
        "BoundedContexts/BusinessSimulations/Application/Queries/ExportLedgerQueryHandler.cs",
    ];

    [Fact]
    public void NoProductCodeGeneratesAPdfWithoutTheGate()
    {
        var apiRoot = LocateApiSourceRoot();
        var offenders = new List<string>();

        foreach (var (relative, text) in ProductSources(apiRoot))
        {
            if (string.Equals(relative, Gate, StringComparison.Ordinal) || Exempt.ContainsKey(relative))
            {
                continue;
            }

            offenders.AddRange(DirectCalls(text).Select(call => $"{relative}: {call}"));
        }

        offenders.Should().BeEmpty(
            "due render QuestPDF sovrapposti nello stesso processo possono dare un PDF a cui manca il testo (#4950, #4933). " +
            "Genera con `QuestPdfRenderGate.GeneratePdf(...)`, che li mette in fila, oppure aggiungi il file a Exempt " +
            $"con il motivo.{Environment.NewLine}  " +
            string.Join($"{Environment.NewLine}  ", offenders));
    }

    /// <summary>
    /// Contro-prova: se l'ago non combaciasse più con come il repo scrive la chiamata, il test sopra passerebbe su zero
    /// candidati. Il cancello deve contenere la chiamata diretta, e i tre renderer di prodotto devono passarne.
    /// </summary>
    [Fact]
    public void TheGateIsWhereQuestPdfGenerates_AndTheThreeRenderersUseIt()
    {
        var apiRoot = LocateApiSourceRoot();
        var sources = ProductSources(apiRoot).ToDictionary(s => s.Relative, s => s.Text, StringComparer.Ordinal);

        sources.Should().ContainKey(Gate, $"il cancello deve esistere in {Gate}");
        DirectCalls(sources[Gate]).Should().NotBeEmpty(
            "il cancello chiama GeneratePdf una volta per forma: se la scansione non trova quella chiamata, non troverebbe " +
            "nemmeno quelle che deve vietare");

        foreach (var renderer in KnownRenderers)
        {
            sources.Should().ContainKey(renderer, $"{renderer} è un renderer di prodotto noto: se è stato spostato, aggiorna l'elenco");
            ThroughTheGate(sources[renderer]).Should().BePositive($"{renderer} deve generare con {GateType}GeneratePdf(...)");
        }
    }

    /// <summary>
    /// Le chiamate dirette a un generatore, esclusi commenti e letterali e quelle che passano dal cancello
    /// (<c>QuestPdfRenderGate.GeneratePdf(</c> contiene l'ago ma è la via corretta).
    /// </summary>
    private static IEnumerable<string> DirectCalls(string text)
    {
        foreach (var generator in Generators)
        {
            foreach (var offset in SourceScanner.FindCodeOccurrences(text, generator))
            {
                if (!text.AsSpan(0, offset).EndsWith(GateType, StringComparison.Ordinal))
                {
                    yield return generator.TrimEnd('(');
                }
            }
        }
    }

    private static int ThroughTheGate(string text) =>
        Generators.Sum(generator => SourceScanner
            .FindCodeOccurrences(text, generator)
            .Count(offset => text.AsSpan(0, offset).EndsWith(GateType, StringComparison.Ordinal)));

    private static IEnumerable<(string Relative, string Text)> ProductSources(string apiRoot)
    {
        foreach (var path in Directory.EnumerateFiles(apiRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(apiRoot, path).Replace('\\', '/');
            if (relative.StartsWith("bin/", StringComparison.Ordinal) || relative.StartsWith("obj/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (relative, File.ReadAllText(path));
        }
    }

    /// <summary>
    /// Risale alla radice del repo. In un worktree <c>.git</c> è un file e non una cartella, quindi valgono entrambi.
    /// </summary>
    private static string LocateApiSourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !Directory.Exists(Path.Combine(dir.FullName, ".git"))
               && !File.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("il binario dei test deve stare dentro il repo meepleai-monorepo");
        var apiRoot = Path.Combine(dir!.FullName, "apps", "api", "src", "Api");
        Directory.Exists(apiRoot).Should().BeTrue($"i sorgenti del prodotto devono esistere in {apiRoot}");
        return apiRoot;
    }
}
