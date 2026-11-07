namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// Applica le metriche (<see cref="ExtractionMetrics"/>) a ciò che un estrattore ha restituito
/// (<see cref="ExtractionObservation"/>) rispetto a ciò che la fixture si aspetta
/// (<see cref="ExtractionExpected"/>). I nomi sono quelli che compaiono nella baseline e nell'artifact.
/// </summary>
internal static class ExtractionScoring
{
    public const string HeadingRecall = "headingRecall";
    public const string ParagraphRecall = "paragraphRecall";
    public const string WordRecall = "wordRecall";
    public const string TableCellRecall = "tableCellRecall";
    public const string TableCellF1 = "tableCellF1";
    public const string ReadingOrder = "readingOrder";
    public const string ScanWordRecall = "scanWordRecall";

    public static SortedDictionary<string, double?> Score(
        ExtractionExpected expected,
        ExtractionObservation observed,
        double ocrEditTolerance)
    {
        // Il testo delle sole pagine solo immagine: è lì che si misura quanto l'estrattore legge una scansione.
        var scannedText = string.Join(
            "\n",
            expected.ScanPages.Select(p => observed.TextByPage.GetValueOrDefault(p, string.Empty)));

        return new SortedDictionary<string, double?>(StringComparer.Ordinal)
        {
            [HeadingRecall] = ExtractionMetrics.HeadingRecall(
                expected.Headings.Select(h => h.Text).ToList(), observed.Headings),
            [ParagraphRecall] = ExtractionMetrics.ParagraphRecall(expected.Paragraphs, observed.FullText),
            [WordRecall] = ExtractionMetrics.WordRecall(expected.ReadingOrder, observed.FullText, ocrEditTolerance),
            [TableCellRecall] = ExtractionMetrics.TableCellRecall(expected.TableCells, observed.FullText),
            [TableCellF1] = ExtractionMetrics.TableCellF1(expected.TableCells, observed.TableCells),
            [ReadingOrder] = ExtractionMetrics.ReadingOrder(expected.ReadingOrder, observed.FullText),
            [ScanWordRecall] = ExtractionMetrics.ScanWordRecall(expected.ScanWords, scannedText, ocrEditTolerance),
        };
    }
}
