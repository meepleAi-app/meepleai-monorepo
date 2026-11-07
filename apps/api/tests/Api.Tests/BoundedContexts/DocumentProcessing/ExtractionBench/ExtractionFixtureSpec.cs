#pragma warning disable MA0048 // File name must match type name — the spec and its supporting records travel together
using System.Text.Json;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// Una fixture del banco di estrazione (#4434 a): il <b>contenuto</b> di un PDF sintetico, in un file
/// JSON in <c>TestData/extraction-bench/fixtures/</c>. Il PDF non si committa: lo genera
/// <see cref="ExtractionFixtureRenderer"/> da questa descrizione, e l'atteso
/// (<see cref="ExtractionExpected"/>) si ricava dalla stessa descrizione. Una sola fonte, quindi
/// contenuto e atteso non possono divergere.
/// </summary>
/// <remarks>
/// Tutto il testo è inventato per il banco (ADR-051/059: niente testo verbatim di regolamenti di
/// terzi). Le lingue sono en, it e de, perché le diacritiche e l'OCR si comportano in modo diverso.
/// </remarks>
/// <param name="Id">Nome stabile della fixture; è la chiave nella baseline.</param>
/// <param name="Language">Codice lingua del testo (en, it, de).</param>
/// <param name="Description">Che cosa deve mettere alla prova.</param>
/// <param name="Pages">Le pagine, in ordine.</param>
internal sealed record ExtractionFixtureSpec(
    string Id,
    string Language,
    string Description,
    IReadOnlyList<FixturePageSpec> Pages)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static ExtractionFixtureSpec Parse(string json) =>
        JsonSerializer.Deserialize<ExtractionFixtureSpec>(json, JsonOptions)
        ?? throw new InvalidOperationException("Fixture JSON is empty");

    public static IReadOnlyList<ExtractionFixtureSpec> LoadAll()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "TestData", "extraction-bench", "fixtures");
        if (!Directory.Exists(dir))
        {
            throw new DirectoryNotFoundException(
                $"Fixture directory not found: {dir}. TestData\\**\\*.json must be copied to the output (Api.Tests.csproj).");
        }

        return Directory.EnumerateFiles(dir, "*.json")
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => Parse(File.ReadAllText(p)))
            .ToList();
    }
}

/// <summary>Una pagina della fixture.</summary>
/// <param name="Number">Numero di pagina, da 1.</param>
/// <param name="Kind"><c>text</c> (ha il livello di testo del PDF) o <c>scan</c> (solo immagine, nessun testo).</param>
/// <param name="Columns">1 o 2. Con 2, i titoli iniziali occupano la larghezza intera e il resto si divide in due colonne.</param>
/// <param name="Blocks">I blocchi, nell'ordine di lettura atteso.</param>
internal sealed record FixturePageSpec(
    int Number,
    string Kind,
    IReadOnlyList<FixtureBlockSpec> Blocks,
    int Columns = 1)
{
    public const string KindText = "text";
    public const string KindScan = "scan";

    public bool IsScan => string.Equals(Kind, KindScan, StringComparison.Ordinal);
}

/// <summary>Un blocco di contenuto.</summary>
/// <param name="Type"><c>heading</c>, <c>paragraph</c> o <c>table</c>.</param>
/// <param name="Text">Il testo di un titolo o di un paragrafo. In un paragrafo <c>{icon}</c> è un'icona in linea.</param>
/// <param name="Level">Il livello di un titolo (1 è il più alto).</param>
/// <param name="Render">Per una tabella: <c>text</c> (celle di testo) o <c>image</c> (disegnata come immagine, senza testo).</param>
/// <param name="Rows">Le righe di una tabella; la prima è l'intestazione.</param>
internal sealed record FixtureBlockSpec(
    string Type,
    string? Text = null,
    int Level = 0,
    string? Render = null,
    IReadOnlyList<IReadOnlyList<string>>? Rows = null)
{
    public const string TypeHeading = "heading";
    public const string TypeParagraph = "paragraph";
    public const string TypeTable = "table";
    public const string IconMarker = "{icon}";

    public bool IsImageTable =>
        string.Equals(Type, TypeTable, StringComparison.Ordinal)
        && string.Equals(Render, "image", StringComparison.Ordinal);
}
