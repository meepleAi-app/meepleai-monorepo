using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using System.Text;

namespace Api.Services;

/// <summary>
/// Service for extracting structured data (tables, diagrams) from PDF documents
/// </summary>
public class PdfTableExtractionService
{
    private readonly ILogger<PdfTableExtractionService> _logger;

    public PdfTableExtractionService(ILogger<PdfTableExtractionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts tables and structured content from a PDF file
    /// </summary>
    public async Task<PdfStructuredExtractionResult> ExtractStructuredContentAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return PdfStructuredExtractionResult.CreateFailure("File path is required");
        }

        if (!File.Exists(filePath))
        {
            return PdfStructuredExtractionResult.CreateFailure($"File not found: {filePath}");
        }

        try
        {
            var result = await Task.Run(() => ExtractStructuredData(filePath), ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return PdfStructuredExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts structured data including tables and images from PDF
    /// </summary>
    private PdfStructuredExtractionResult ExtractStructuredData(string filePath)
    {
        var tables = new List<PdfTable>();
        var diagrams = new List<PdfDiagram>();
        var atomicRules = new List<string>();

        using var pdfReader = new PdfReader(filePath);
        using var pdfDoc = new PdfDocument(pdfReader);

        var pageCount = pdfDoc.GetNumberOfPages();

        for (int pageNum = 1; pageNum <= pageCount; pageNum++)
        {
            var page = pdfDoc.GetPage(pageNum);

            // Extract text with location information
            var strategy = new LocationTextExtractionStrategy();
            var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);

            // Analyze page for table-like structures
            var pageTables = DetectTablesInPage(pageText, pageNum);
            tables.AddRange(pageTables);

            // Convert table rows to atomic rules
            foreach (var table in pageTables)
            {
                var rules = ConvertTableToAtomicRules(table);
                atomicRules.AddRange(rules);
            }

            // Detect images/diagrams (PDF-03 requirement)
            var pageDiagrams = DetectDiagramsInPage(page, pageNum);
            diagrams.AddRange(pageDiagrams);
        }

        _logger.LogInformation(
            "Extracted structured content from PDF: {FilePath}, Tables: {TableCount}, Diagrams: {DiagramCount}, Atomic Rules: {RuleCount}",
            filePath, tables.Count, diagrams.Count, atomicRules.Count);

        return PdfStructuredExtractionResult.CreateSuccess(tables, diagrams, atomicRules);
    }

    /// <summary>
    /// Detects table-like structures in page text
    /// Uses heuristics to identify structured data patterns
    /// </summary>
    private List<PdfTable> DetectTablesInPage(string pageText, int pageNum)
    {
        var tables = new List<PdfTable>();

        if (string.IsNullOrWhiteSpace(pageText))
        {
            return tables;
        }

        var lines = pageText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Simple table detection: look for lines with multiple tab-separated or space-separated values
        var currentTable = new List<string[]>();
        var isInTable = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                if (isInTable && currentTable.Count > 1)
                {
                    // End of table detected
                    tables.Add(CreateTableFromRows(currentTable, pageNum, i - currentTable.Count + 1));
                    currentTable.Clear();
                    isInTable = false;
                }
                continue;
            }

            // Detect potential table row (contains multiple columns)
            // Heuristic: line with multiple spaces suggesting column separation
            var columns = SplitIntoColumns(line);

            if (columns.Length >= 2)
            {
                currentTable.Add(columns);
                isInTable = true;
            }
            else if (isInTable && currentTable.Count > 1)
            {
                // End of table
                tables.Add(CreateTableFromRows(currentTable, pageNum, i - currentTable.Count));
                currentTable.Clear();
                isInTable = false;
            }
        }

        // Handle table that ends at page end
        if (currentTable.Count > 1)
        {
            tables.Add(CreateTableFromRows(currentTable, pageNum, lines.Length - currentTable.Count));
        }

        return tables;
    }

    /// <summary>
    /// Splits a line into columns based on spacing patterns
    /// </summary>
    private string[] SplitIntoColumns(string line)
    {
        // Split by 2+ spaces or tabs as column separators
        var columns = System.Text.RegularExpressions.Regex
            .Split(line, @"\s{2,}|\t+")
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToArray();

        return columns;
    }

    /// <summary>
    /// Creates a PdfTable object from detected rows
    /// </summary>
    private PdfTable CreateTableFromRows(List<string[]> rows, int pageNum, int startLine)
    {
        var headers = rows.Count > 0 ? rows[0] : Array.Empty<string>();
        var dataRows = rows.Count > 1 ? rows.Skip(1).ToList() : new List<string[]>();

        return new PdfTable
        {
            PageNumber = pageNum,
            StartLine = startLine,
            Headers = headers.ToList(),
            Rows = dataRows,
            ColumnCount = headers.Length,
            RowCount = dataRows.Count
        };
    }

    /// <summary>
    /// Converts table rows to atomic rules (PDF-03 requirement)
    /// Each row becomes a structured rule statement
    /// </summary>
    private List<string> ConvertTableToAtomicRules(PdfTable table)
    {
        var rules = new List<string>();

        if (table.Headers.Count == 0 || table.Rows.Count == 0)
        {
            return rules;
        }

        foreach (var row in table.Rows)
        {
            // Create a rule from row data
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

    /// <summary>
    /// Detects diagrams and images in a PDF page (PDF-03 requirement)
    /// </summary>
    private List<PdfDiagram> DetectDiagramsInPage(iText.Kernel.Pdf.PdfPage page, int pageNum)
    {
        var diagrams = new List<PdfDiagram>();

        try
        {
            // Extract images from page
            var imageExtractor = new ImageExtractionStrategy();
            PdfCanvasProcessor processor = new PdfCanvasProcessor(imageExtractor);
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract images from page {PageNum}", pageNum);
        }

        return diagrams;
    }
}

/// <summary>
/// Custom extraction strategy for images
/// </summary>
public class ImageExtractionStrategy : IEventListener
{
    private readonly List<ExtractedImage> _images = new();

    public List<ExtractedImage> GetImages() => _images;

    public void EventOccurred(IEventData data, EventType type)
    {
        if (type == EventType.RENDER_IMAGE)
        {
            var renderInfo = data as ImageRenderInfo;
            if (renderInfo != null)
            {
                try
                {
                    var image = renderInfo.GetImage();
                    if (image != null)
                    {
                        var imageBytes = image.GetImageBytes();
                        var pdfImage = image.GetPdfObject();
                        var width = pdfImage?.GetAsNumber(iText.Kernel.Pdf.PdfName.Width)?.IntValue() ?? 0;
                        var height = pdfImage?.GetAsNumber(iText.Kernel.Pdf.PdfName.Height)?.IntValue() ?? 0;

                        _images.Add(new ExtractedImage
                        {
                            Width = width,
                            Height = height,
                            Data = imageBytes
                        });
                    }
                }
                catch (Exception)
                {
                    // Ignore errors for individual images
                }
            }
        }
    }

    public ICollection<EventType> GetSupportedEvents()
    {
        return new HashSet<EventType> { EventType.RENDER_IMAGE };
    }
}

public class ExtractedImage
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Represents a table extracted from a PDF
/// </summary>
public class PdfTable
{
    public int PageNumber { get; set; }
    public int StartLine { get; set; }
    public List<string> Headers { get; set; } = new();
    public List<string[]> Rows { get; set; } = new();
    public int ColumnCount { get; set; }
    public int RowCount { get; set; }
}

/// <summary>
/// Represents a diagram/image extracted from a PDF
/// </summary>
public class PdfDiagram
{
    public int PageNumber { get; set; }
    public string DiagramType { get; set; } = "Unknown";
    public string Description { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[]? ImageData { get; set; }
}

/// <summary>
/// Result of structured PDF extraction
/// </summary>
public record PdfStructuredExtractionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<PdfTable> Tables { get; init; } = new();
    public List<PdfDiagram> Diagrams { get; init; } = new();
    public List<string> AtomicRules { get; init; } = new();
    public int TableCount => Tables.Count;
    public int DiagramCount => Diagrams.Count;
    public int AtomicRuleCount => AtomicRules.Count;

    public static PdfStructuredExtractionResult CreateSuccess(
        List<PdfTable> tables,
        List<PdfDiagram> diagrams,
        List<string> atomicRules) =>
        new()
        {
            Success = true,
            Tables = tables,
            Diagrams = diagrams,
            AtomicRules = atomicRules
        };

    public static PdfStructuredExtractionResult CreateFailure(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
