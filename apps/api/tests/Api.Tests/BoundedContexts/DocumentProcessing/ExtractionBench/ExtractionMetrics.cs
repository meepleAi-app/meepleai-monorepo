namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// Le metriche del banco di qualità dell'estrazione (#4434 a), come funzioni pure.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ogni metrica restituisce <c>double?</c>, e <c>null</c> significa «non applicabile»</b> — mai
/// uno zero fittizio. I due motivi sono distinti e vanno tenuti distinti:
/// </para>
/// <list type="bullet">
///   <item><description><b>la fixture non prevede la cosa</b> (nessun titolo atteso, nessuna tabella);</description></item>
///   <item><description><b>l'estrattore non può darla</b>: Docnet restituisce testo e basta, quindi non
///   classifica titoli (<see cref="HeadingRecall"/>) né espone celle (<see cref="TableCellF1"/>). Scrivere
///   «0 titoli trovati» sarebbe un numero falso che un domani qualcuno proverebbe a «migliorare».</description></item>
/// </list>
/// <para>
/// Quando invece l'estrattore sa distinguere la cosa e non ne trova nessuna (un elenco di titoli
/// vuoto, non assente), il risultato è <c>0</c>: lì lo zero è vero.
/// </para>
/// <para>
/// Le metriche <i>di testo</i> (<see cref="ParagraphRecall"/>, <see cref="TableCellRecall"/>,
/// <see cref="ReadingOrder"/>, <see cref="ScanWordRecall"/>) valgono per ogni estrattore; quelle
/// <i>strutturali</i> (<see cref="HeadingRecall"/>, <see cref="TableCellF1"/>) solo per chi espone
/// elementi. Tutte usano <see cref="ExtractionTextMatcher"/> come criterio.
/// </para>
/// </remarks>
internal static class ExtractionMetrics
{
    /// <summary>
    /// Quota dei titoli attesi che compaiono fra i titoli osservati (uguali o contenuti in uno di essi:
    /// l'estrattore può accorpare un numero di sezione). <c>null</c> se
    /// <paramref name="observedHeadings"/> è <c>null</c>, cioè l'estrattore non classifica gli elementi.
    /// </summary>
    /// <remarks>Il livello del titolo non si confronta: <c>ExtractedElement</c> non lo porta.</remarks>
    public static double? HeadingRecall(IReadOnlyList<string> expectedHeadings, IReadOnlyList<string>? observedHeadings)
    {
        if (expectedHeadings.Count == 0 || observedHeadings is null)
        {
            return null;
        }

        var observed = observedHeadings.Select(ExtractionTextMatcher.Squeeze).ToList();
        var found = expectedHeadings
            .Select(ExtractionTextMatcher.Squeeze)
            .Count(h => observed.Any(o => ExtractionTextMatcher.ContainsPhrase(o, h)));
        return (double)found / expectedHeadings.Count;
    }

    /// <summary>
    /// Quota dei paragrafi attesi ritrovati, contigui, nel testo osservato, <b>senza guardare gli
    /// spazi</b>: dice se il contenuto c'è e in che ordine, non se le parole sono rimaste separate
    /// (per quello c'è <see cref="WordRecall"/>).
    /// </summary>
    public static double? ParagraphRecall(IReadOnlyList<string> expectedParagraphs, string observedText) =>
        PhraseRecall(expectedParagraphs, observedText);

    /// <summary>
    /// Quota delle parole distinte dei testi attesi che compaiono, <b>a parola intera</b>, nel testo
    /// osservato. È la metrica graduata e sensibile alle parole fuse: una parola persa per uno spazio
    /// mancante («alongthe») la abbassa di una parola, non di un paragrafo intero. Con tolleranza zero,
    /// come per <see cref="ScanWordRecall"/>.
    /// </summary>
    public static double? WordRecall(IReadOnlyList<string> expectedTexts, string observedText, double ocrEditTolerance)
    {
        var words = expectedTexts
            .SelectMany(t => ExtractionTextMatcher.Words(ExtractionTextMatcher.Normalize(t)))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return ScanWordRecall(words, observedText, ocrEditTolerance);
    }

    /// <summary>
    /// Quota delle celle attese ritrovate nel testo osservato. È una metrica <b>di testo</b>: dice se il
    /// contenuto della tabella sopravvive all'estrazione, non se la struttura righe/colonne è giusta
    /// (per quello c'è <see cref="TableCellF1"/>).
    /// </summary>
    public static double? TableCellRecall(IReadOnlyList<string> expectedCells, string observedText) =>
        PhraseRecall(expectedCells, observedText);

    /// <summary>
    /// F1 sulle celle come insieme di stringhe normalizzate (multiinsieme: due celle «1» attese non
    /// sono coperte da una sola osservata). <c>null</c> se l'estrattore non espone le celle.
    /// </summary>
    public static double? TableCellF1(IReadOnlyList<string> expectedCells, IReadOnlyList<string>? observedCells)
    {
        if (expectedCells.Count == 0 || observedCells is null)
        {
            return null;
        }

        var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cell in observedCells.Select(ExtractionTextMatcher.Normalize))
        {
            remaining[cell] = remaining.GetValueOrDefault(cell) + 1;
        }

        var truePositives = 0;
        foreach (var cell in expectedCells.Select(ExtractionTextMatcher.Normalize))
        {
            if (remaining.TryGetValue(cell, out var left) && left > 0)
            {
                remaining[cell] = left - 1;
                truePositives++;
            }
        }

        if (truePositives == 0)
        {
            return 0.0;
        }

        var precision = (double)truePositives / observedCells.Count;
        var recall = (double)truePositives / expectedCells.Count;
        return 2 * precision * recall / (precision + recall);
    }

    /// <summary>
    /// Ordine di lettura: per ogni coppia di indicatori consecutivi della sequenza attesa, 1 se nel testo
    /// osservato il primo precede il secondo, 0 se sono invertiti o se uno manca; media sulle coppie.
    /// Misura le due colonne: colonne interfogliate o lette per riga fanno scendere il valore.
    /// </summary>
    public static double? ReadingOrder(IReadOnlyList<string> expectedSequence, string observedText)
    {
        if (expectedSequence.Count < 2)
        {
            return null;
        }

        var text = ExtractionTextMatcher.Squeeze(observedText);
        var positions = expectedSequence
            .Select(m => text.IndexOf(ExtractionTextMatcher.Squeeze(m), StringComparison.Ordinal))
            .ToList();

        var inOrder = 0;
        for (var i = 0; i < positions.Count - 1; i++)
        {
            if (positions[i] >= 0 && positions[i + 1] >= 0 && positions[i] < positions[i + 1])
            {
                inOrder++;
            }
        }

        return (double)inOrder / (positions.Count - 1);
    }

    /// <summary>
    /// Quota delle parole attese su una pagina scansionata che l'estrattore ha restituito, a parola
    /// intera. Zero è un risultato vero (Docnet non fa OCR: #4437), non un errore.
    /// </summary>
    public static double? ScanWordRecall(IReadOnlyList<string> expectedWords, string observedText, double ocrEditTolerance)
    {
        if (expectedWords.Count == 0)
        {
            return null;
        }

        var observed = ExtractionTextMatcher.Words(ExtractionTextMatcher.Normalize(observedText));
        var found = expectedWords
            .Select(ExtractionTextMatcher.Normalize)
            .Count(w => ExtractionTextMatcher.WordMatches(w, observed, ocrEditTolerance));
        return (double)found / expectedWords.Count;
    }

    private static double? PhraseRecall(IReadOnlyList<string> expectedPhrases, string observedText)
    {
        if (expectedPhrases.Count == 0)
        {
            return null;
        }

        var text = ExtractionTextMatcher.Squeeze(observedText);
        var found = expectedPhrases
            .Select(ExtractionTextMatcher.Squeeze)
            .Count(p => ExtractionTextMatcher.ContainsPhrase(text, p));
        return (double)found / expectedPhrases.Count;
    }
}
