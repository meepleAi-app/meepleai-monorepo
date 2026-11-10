using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// La <b>vista normalizzata</b> di ciò che un estrattore ha restituito per una fixture (#4434 a): la
/// sola forma su cui le metriche lavorano, uguale per tutti gli estrattori.
/// </summary>
/// <remarks>
/// <para>
/// Gli estrattori non restituiscono la stessa cosa. Docnet dà testo per pagina; Unstructured dà
/// elementi con una categoria. La vista tiene i campi che hanno senso per entrambi come valori, e
/// quelli che ha senso chiedere a uno solo come <c>null</c> (= «questo estrattore non lo sa dire»,
/// che <see cref="ExtractionMetrics"/> traduce in «metrica non applicabile», mai in uno zero):
/// </para>
/// <code>
///                 Docnet                          Unstructured
/// FullText        testo delle pagine, in ordine   testi degli elementi, nell'ordine del documento
/// TextByPage      pagine reali (da 1), anche      pagine lette dagli elementi (PageNumber);
///                 quelle vuote                    nessuna riga per una pagina senza elementi
/// Headings        null (non classifica)           elementi di categoria «Title»; lista vuota se nessuno
/// TableCells      null (solo testo)               null: il servizio non serializza text_as_html (#4439),
///                                                 quindi nessuna cella strutturata arriva alla API
/// </code>
/// <para>
/// Quando #4439 farà arrivare le celle, <c>TableCells</c> si popola per Unstructured e
/// <see cref="ExtractionMetrics.TableCellF1"/> diventa applicabile senza toccare le metriche.
/// </para>
/// </remarks>
/// <param name="FullText">Tutto il testo restituito, nell'ordine di lettura dell'estrattore.</param>
/// <param name="TextByPage">Il testo di ogni pagina, per numero di pagina.</param>
/// <param name="Headings">I titoli riconosciuti, o <c>null</c> se l'estrattore non classifica gli elementi.</param>
/// <param name="TableCells">Le celle riconosciute, o <c>null</c> se l'estrattore non le espone.</param>
/// <param name="PageCount">Le pagine del documento secondo l'estrattore.</param>
internal sealed record ExtractionObservation(
    string FullText,
    IReadOnlyDictionary<int, string> TextByPage,
    IReadOnlyList<string>? Headings,
    IReadOnlyList<string>? TableCells,
    int PageCount)
{
    public static ExtractionObservation FromDocnet(PagedTextExtractionResult result)
    {
        EnsureSuccess(result);
        var pages = result.PageChunks.OrderBy(p => p.PageNumber).ToList();
        return new ExtractionObservation(
            FullText: string.Join("\n", pages.Select(p => p.Text)),
            TextByPage: pages.ToDictionary(p => p.PageNumber, p => p.Text),
            Headings: null,
            TableCells: null,
            PageCount: result.TotalPages);
    }

    public static ExtractionObservation FromUnstructured(PagedTextExtractionResult result)
    {
        EnsureSuccess(result);
        var elements = result.StructuredElements ?? [];
        return new ExtractionObservation(
            FullText: string.Join("\n", elements.Select(e => e.Text)),
            TextByPage: elements
                .GroupBy(e => e.PageNumber)
                .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => e.Text))),
            Headings: elements
                .Where(e => string.Equals(e.ElementType, "Title", StringComparison.Ordinal))
                .Select(e => e.Text)
                .ToList(),
            TableCells: null,
            PageCount: result.TotalPages);
    }

    private static void EnsureSuccess(PagedTextExtractionResult result)
    {
        if (!result.Success)
        {
            // Un estrattore che fallisce non è «0 su ogni metrica»: è un banco che non ha misurato.
            throw new InvalidOperationException($"Extractor failed: {result.ErrorMessage}");
        }
    }
}
