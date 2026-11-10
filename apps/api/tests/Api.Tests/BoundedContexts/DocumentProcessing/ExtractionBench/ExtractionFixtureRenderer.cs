using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Tests.BoundedContexts.DocumentProcessing.ExtractionBench;

/// <summary>
/// Genera il PDF di una fixture (#4434 a) con QuestPDF, già dipendenza di <c>Api.Tests</c>: nessun
/// pacchetto nuovo e nessun binario committato. Come per <c>tests/fixtures/make-text-pdf.py</c>, il
/// testo estratto è l'asserzione, quindi si genera dal sorgente leggibile.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pagine «solo immagine»</b> (scansioni e tabelle disegnate come grafica): QuestPDF sa rasterizzare i
/// propri documenti (<c>GenerateImages</c>), quindi il contenuto si compone come testo in un documento
/// d'appoggio, si rasterizza in PNG e il PNG si incorpora nel PDF finale. Il risultato non ha livello
/// di testo, ma i pixel ci sono: <c>ExtractionFixtureTests</c> lo prova su entrambi i fronti.
/// Il font è quello incorporato in QuestPDF (Lato), non quelli del sistema, così la rasterizzazione non
/// dipende dal runner.
/// </para>
/// <para>
/// <b>Icone in linea</b>: <c>{icon}</c> in un paragrafo diventa un piccolo quadrato vettoriale dentro la
/// riga (nessun carattere, nessun glifo): ciò che si misura è se l'estrattore tiene intera la frase
/// attorno a un oggetto che non è testo.
/// </para>
/// </remarks>
internal static class ExtractionFixtureRenderer
{
    private const float PageMargin = 28f;
    private const int RasterDpi = 220;

    public static byte[] RenderPdf(ExtractionFixtureSpec spec) =>
        Document.Create(container =>
        {
            foreach (var page in spec.Pages)
            {
                container.Page(p =>
                {
                    p.Size(PageSizes.A5);
                    if (page.IsScan)
                    {
                        p.Margin(0);
                        p.Content().Image(RasterizePage(page)).FitArea();
                    }
                    else
                    {
                        p.Margin(PageMargin);
                        p.DefaultTextStyle(t => t.FontSize(10.5f));
                        p.Content().Element(c => ComposePage(c, page));
                    }
                });
            }
        }).GeneratePdf();

    internal static byte[] RasterizePage(FixturePageSpec page) =>
        Rasterize(
            document => document.Page(p =>
            {
                p.Size(PageSizes.A5);
                p.Margin(PageMargin);
                p.DefaultTextStyle(t => t.FontSize(10.5f));
                p.Content().Element(c => ComposeColumn(c, page.Blocks));
            }));

    internal static byte[] RasterizeTable(FixtureBlockSpec table) =>
        Rasterize(
            document => document.Page(p =>
            {
                p.ContinuousSize(PageSizes.A5.Width - (2 * PageMargin));
                p.Margin(4);
                p.DefaultTextStyle(t => t.FontSize(10.5f));
                p.Content().Element(c => ComposeTable(c, table));
            }));

    private static byte[] Rasterize(Action<IDocumentContainer> compose)
    {
        var images = Document.Create(compose)
            .GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = RasterDpi })
            .ToList();
        return images.Count > 0
            ? images[0]
            : throw new InvalidOperationException("QuestPDF GenerateImages produced no image");
    }

    private static void ComposePage(IContainer container, FixturePageSpec page)
    {
        if (page.Columns < 2)
        {
            ComposeColumn(container, page.Blocks);
            return;
        }

        // Titoli iniziali a tutta larghezza, il resto diviso a metà fra due colonne: l'ordine di lettura
        // atteso è l'ordine del file (prima la colonna sinistra, poi la destra).
        var leading = page.Blocks.TakeWhile(b => b.Type == FixtureBlockSpec.TypeHeading).ToList();
        var rest = page.Blocks.Skip(leading.Count).ToList();
        var left = rest.Take((rest.Count + 1) / 2).ToList();
        var right = rest.Skip(left.Count).ToList();

        container.Column(col =>
        {
            col.Spacing(8);
            foreach (var block in leading)
            {
                col.Item().Element(c => ComposeBlock(c, block));
            }

            col.Item().Row(row =>
            {
                row.Spacing(18);
                row.RelativeItem().Element(c => ComposeColumn(c, left));
                row.RelativeItem().Element(c => ComposeColumn(c, right));
            });
        });
    }

    private static void ComposeColumn(IContainer container, IReadOnlyList<FixtureBlockSpec> blocks) =>
        container.Column(col =>
        {
            col.Spacing(7);
            foreach (var block in blocks)
            {
                col.Item().Element(c => ComposeBlock(c, block));
            }
        });

    private static void ComposeBlock(IContainer container, FixtureBlockSpec block)
    {
        switch (block.Type)
        {
            case FixtureBlockSpec.TypeHeading:
                container.Text(block.Text!).Bold().FontSize(block.Level <= 1 ? 17f : block.Level == 2 ? 13f : 11.5f);
                break;
            case FixtureBlockSpec.TypeParagraph:
                container.Text(text => ComposeParagraph(text, block.Text!));
                break;
            case FixtureBlockSpec.TypeTable when block.IsImageTable:
                container.Image(RasterizeTable(block)).FitWidth();
                break;
            case FixtureBlockSpec.TypeTable:
                ComposeTable(container, block);
                break;
            default:
                throw new InvalidOperationException($"Unknown block type '{block.Type}'");
        }
    }

    private static void ComposeParagraph(TextDescriptor text, string content)
    {
        var parts = content.Split(FixtureBlockSpec.IconMarker);
        for (var i = 0; i < parts.Length; i++)
        {
            text.Span(parts[i]);
            if (i < parts.Length - 1)
            {
                text.Element(e => e.Width(8).Height(8).Background(Colors.Orange.Medium), TextInjectedElementAlignment.AboveBaseline);
            }
        }
    }

    private static void ComposeTable(IContainer container, FixtureBlockSpec block)
    {
        var rows = block.Rows!;
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                for (var i = 0; i < rows[0].Count; i++)
                {
                    columns.RelativeColumn();
                }
            });

            for (var r = 0; r < rows.Count; r++)
            {
                foreach (var cell in rows[r])
                {
                    var content = table.Cell().Border(0.6f).Padding(4);
                    if (r == 0)
                    {
                        content.Text(cell).Bold();
                    }
                    else
                    {
                        content.Text(cell);
                    }
                }
            }
        });
    }
}
