using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace Api.Services.Pdf;

/// <summary>
/// Service responsible for analyzing table structure and converting to atomic rules
/// </summary>
internal class TableStructureAnalyzer : ITableStructureAnalyzer
{
    private readonly ILogger<TableStructureAnalyzer> _logger;

    public TableStructureAnalyzer(ILogger<TableStructureAnalyzer> logger)
    {
        _logger = logger;
    }

    public List<string> ConvertTableToAtomicRules(PdfTable table)
    {
        var rules = new List<string>();

        if (table.Headers.Count == 0 || table.Rows.Count == 0)
        {
            return rules;
        }

        foreach (var row in table.Rows)
        {
            var ruleParts = new List<string>();

            for (int i = 0; i < Math.Min(table.Headers.Count, row.Length); i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                {
                    ruleParts.Add($"{table.Headers[i]}: {row[i]}");
                }
            }

            if (ruleParts.Count > 0)
            {
                var rule = $"[Table on page {table.PageNumber}] {string.Join("; ", ruleParts)}";
                rules.Add(rule);
            }
        }

        return rules;
    }

    public List<PdfDiagram> DetectDiagramsInPage(PdfPage page, int pageNum)
    {
        var diagrams = new List<PdfDiagram>();

        try
        {
            var imageExtractor = new ImageExtractionStrategy();
            var processor = new PdfCanvasProcessor(imageExtractor);
            processor.ProcessPageContent(page);

            var images = imageExtractor.GetImages();

            for (int i = 0; i < images.Count; i++)
            {
                diagrams.Add(new PdfDiagram
                {
                    PageNumber = pageNum,
                    DiagramType = "Image",
                    Description = $"Image {i + 1} on page {pageNum}",
                    Width = images[i].Width,
                    Height = images[i].Height,
                    ImageData = images[i].Data
                });
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation extracting images from page {PageNum}", pageNum);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogWarning(ex, "Unsupported image format on page {PageNum}", pageNum);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "I/O error extracting images from page {PageNum}", pageNum);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // DATA ROBUSTNESS PATTERN: Page-level diagram extraction failures should not stop PDF processing
            // Rationale: Diagram extraction from PDF pages can encounter various runtime exceptions (corrupt
            // images, unsupported formats, malformed page structures). Failing on one page should not prevent
            // extracting diagrams from other pages. We log the error and return empty list for this page.
            // Context: iText7 PdfCanvasProcessor can throw unexpected exceptions during image extraction
            _logger.LogWarning(ex, "Unexpected error extracting images from page {PageNum}", pageNum);
        }
#pragma warning restore CA1031 // Do not catch general exception types

        return diagrams;
    }
}
