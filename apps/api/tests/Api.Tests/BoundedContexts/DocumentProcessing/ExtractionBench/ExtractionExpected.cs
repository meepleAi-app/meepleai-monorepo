#pragma warning disable MA0048 // File name must match type name — the expected record and its heading record travel together
namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>Un titolo atteso.</summary>
/// <param name="Text">Il testo.</param>
/// <param name="Level">Il livello dichiarato nella fixture. Non si confronta: <c>ExtractedElement</c> non lo porta.</param>
/// <param name="Page">La pagina.</param>
internal sealed record ExpectedHeading(string Text, int Level, int Page);

/// <summary>
/// Ciò che un estrattore deve restituire per una fixture, ricavato dal suo <see cref="ExtractionFixtureSpec"/>.
/// </summary>
/// <param name="Headings">Titoli delle pagine con livello di testo.</param>
/// <param name="Paragraphs">Paragrafi delle pagine con livello di testo, con le icone in linea tolte.</param>
/// <param name="ReadingOrder">Titoli e paragrafi nell'ordine di lettura atteso (colonna 1, poi colonna 2).</param>
/// <param name="TableCells">Tutte le celle di tutte le tabelle, intestazione compresa (anche quelle disegnate come immagine).</param>
/// <param name="ScanWords">Le parole scritte nelle pagine solo immagine.</param>
/// <param name="ScanPages">I numeri delle pagine solo immagine.</param>
internal sealed record ExtractionExpected(
    IReadOnlyList<ExpectedHeading> Headings,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<string> ReadingOrder,
    IReadOnlyList<string> TableCells,
    IReadOnlyList<string> ScanWords,
    IReadOnlyList<int> ScanPages)
{
    public static ExtractionExpected From(ExtractionFixtureSpec spec)
    {
        var headings = new List<ExpectedHeading>();
        var paragraphs = new List<string>();
        var order = new List<string>();
        var cells = new List<string>();
        var scanWords = new List<string>();
        var scanPages = new List<int>();

        foreach (var page in spec.Pages)
        {
            if (page.IsScan)
            {
                scanPages.Add(page.Number);
                foreach (var text in page.Blocks.Select(b => b.Text).Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    scanWords.AddRange(ExtractionTextMatcher.Words(ExtractionTextMatcher.Normalize(text)));
                }

                continue;
            }

            foreach (var block in page.Blocks)
            {
                switch (block.Type)
                {
                    case FixtureBlockSpec.TypeHeading:
                        headings.Add(new ExpectedHeading(block.Text!, block.Level, page.Number));
                        order.Add(block.Text!);
                        break;
                    case FixtureBlockSpec.TypeParagraph:
                        var withoutIcons = block.Text!.Replace(FixtureBlockSpec.IconMarker, " ", StringComparison.Ordinal);
                        paragraphs.Add(withoutIcons);
                        order.Add(withoutIcons);
                        break;
                    case FixtureBlockSpec.TypeTable:
                        cells.AddRange(block.Rows!.SelectMany(r => r));
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown block type '{block.Type}' in fixture '{spec.Id}'");
                }
            }
        }

        return new ExtractionExpected(headings, paragraphs, order, cells, scanWords.Distinct(StringComparer.Ordinal).ToList(), scanPages);
    }
}
