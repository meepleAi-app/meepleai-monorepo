using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;
using System.Linq;

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
    public virtual async Task<PdfStructuredExtractionResult> ExtractStructuredContentAsync(
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

            // Analyze page for table-like structures
            var pageLines = ExtractPageLines(page);
            var pageTables = DetectTablesInPage(pageLines, pageNum);
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
    private List<PdfTable> DetectTablesInPage(List<PositionedTextLine> lines, int pageNum)
    {
        var tables = new List<PdfTable>();

        if (lines.Count == 0)
        {
            return tables;
        }

        var currentRows = new List<string[]>();
        List<ColumnBoundary>? currentBoundaries = null;
        var tableStartLine = -1;
        var lastLineWasBlank = false;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmedText = line.GetTrimmedText();

            if (string.IsNullOrWhiteSpace(trimmedText))
            {
                if (currentBoundaries != null && currentBoundaries.Count > 0 && currentRows.Count > 0)
                {
                    currentRows.Add(Enumerable.Repeat(string.Empty, currentBoundaries.Count).ToArray());
                }
                else
                {
                    FinalizeCurrentTable();
                }

                lastLineWasBlank = true;
                continue;
            }

            if (currentBoundaries != null && lastLineWasBlank)
            {
                var previewSplit = SplitIntoColumns(line, null);
                var previewColumnCount = Math.Max(previewSplit.Columns.Count, previewSplit.Boundaries.Count);
                var hasBlankRowSentinel = currentRows.Count > 0 && currentRows[^1].All(string.IsNullOrWhiteSpace);
                var previewNonEmptyColumns = previewSplit.Columns.Count(value => !string.IsNullOrWhiteSpace(value));
                var requiredPreviewColumns = currentBoundaries.Count > 0
                    ? Math.Max(2, (currentBoundaries.Count + 1) / 2)
                    : 0;

                if (previewColumnCount > 0 && previewColumnCount < currentBoundaries.Count)
                {
                    FinalizeCurrentTable();
                    i--;
                    lastLineWasBlank = false;
                    continue;
                }

                if (hasBlankRowSentinel && currentRows.Count > 1 && previewNonEmptyColumns >= requiredPreviewColumns)
                {
                    FinalizeCurrentTable();
                    i--;
                    lastLineWasBlank = false;
                    continue;
                }
            }

            lastLineWasBlank = false;

            if (currentBoundaries == null)
            {
                var split = SplitIntoColumns(line, null);

                if (split.Columns.Count >= 2)
                {
                    currentBoundaries = split.Boundaries;
                    tableStartLine = i;
                    currentRows.Add(NormalizeRow(split.Columns, currentBoundaries.Count));
                }
                else
                {
                    FinalizeCurrentTable();
                }
            }
            else
            {
                if (currentBoundaries.Count > 0 && line.Characters.Count > 0)
                {
                    var rowStart = line.Characters.Min(c => c.X);
                    var rowEnd = line.Characters.Max(c => c.EndX);
                    var firstBoundary = currentBoundaries[0];
                    var lastBoundary = currentBoundaries[^1];
                    var driftTolerance = Math.Max(8f, line.GetAverageCharacterWidth() * 3f);

                    var extendsLeft = rowStart < firstBoundary.Start - driftTolerance;
                    var extendsRight = rowEnd > lastBoundary.End + driftTolerance;

                    if (extendsLeft || extendsRight)
                    {
                        FinalizeCurrentTable();
                        i--;
                        continue;
                    }
                }

                var split = SplitIntoColumns(line, currentBoundaries);
                var boundariesForRow = split.Boundaries;
                var columnCount = boundariesForRow.Count;

                if (columnCount > currentBoundaries.Count)
                {
                    for (int r = 0; r < currentRows.Count; r++)
                    {
                        currentRows[r] = NormalizeRow(currentRows[r], columnCount);
                    }
                }

                currentBoundaries = boundariesForRow;

                var normalizedRow = NormalizeRow(split.Columns, columnCount);
                var hasContent = normalizedRow.Any(value => !string.IsNullOrWhiteSpace(value));

                if (hasContent)
                {
                    currentRows.Add(normalizedRow);
                }
                else
                {
                    FinalizeCurrentTable();
                }
            }
        }

        FinalizeCurrentTable();

        return tables;

        void FinalizeCurrentTable()
        {
            TrimTrailingEmptyRows();

            if (currentRows.Count > 1 && currentBoundaries != null)
            {
                tables.Add(CreateTableFromRows(
                    currentRows,
                    pageNum,
                    tableStartLine >= 0 ? tableStartLine : 0,
                    currentBoundaries.Count));
            }

            currentRows.Clear();
            currentBoundaries = null;
            tableStartLine = -1;
            lastLineWasBlank = false;
        }

        void TrimTrailingEmptyRows()
        {
            while (currentRows.Count > 0 && currentRows[^1].All(value => string.IsNullOrWhiteSpace(value)))
            {
                currentRows.RemoveAt(currentRows.Count - 1);
            }
        }
    }

    /// <summary>
    /// Splits a line into columns based on character positioning
    /// </summary>
    private ColumnSplitResult SplitIntoColumns(PositionedTextLine line, List<ColumnBoundary>? existingBoundaries)
    {
        var result = new ColumnSplitResult();

        if (line.Characters.Count == 0)
        {
            if (existingBoundaries != null)
            {
                result.Boundaries = existingBoundaries;
                result.Columns = Enumerable.Repeat(string.Empty, existingBoundaries.Count).ToList();
            }

            return result;
        }

        var boundaries = existingBoundaries != null && existingBoundaries.Count > 0
            ? existingBoundaries.Select(b => b.Clone()).ToList()
            : DetectColumnBoundaries(line);

        if (boundaries.Count == 0)
        {
            var text = line.GetTrimmedText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Columns.Add(text);
            }

            return result;
        }

        boundaries = boundaries
            .OrderBy(b => b.Start)
            .ToList();

        var tolerance = Math.Max(2f, line.GetAverageCharacterWidth() * 0.75f);
        var columnTexts = boundaries.Select(_ => new StringBuilder()).ToList();

        foreach (var character in line.Characters)
        {
            var columnIndex = FindBoundaryIndex(boundaries, character, tolerance);

            if (columnIndex == -1)
            {
                var newBoundary = new ColumnBoundary
                {
                    Start = character.X - tolerance,
                    End = character.EndX + tolerance
                };

                var insertIndex = boundaries.FindIndex(b => newBoundary.Start < b.Start);
                if (insertIndex < 0)
                {
                    boundaries.Add(newBoundary);
                    columnTexts.Add(new StringBuilder());
                    columnIndex = boundaries.Count - 1;
                }
                else
                {
                    boundaries.Insert(insertIndex, newBoundary);
                    columnTexts.Insert(insertIndex, new StringBuilder());
                    columnIndex = insertIndex;
                }
            }

            var boundary = boundaries[columnIndex];
            boundary.Start = Math.Min(boundary.Start, character.X - tolerance);
            boundary.End = Math.Max(boundary.End, character.EndX + tolerance);
            columnTexts[columnIndex].Append(character.Text);
        }

        result.Boundaries = boundaries;
        result.Columns = columnTexts.Select(sb => sb.ToString().Trim()).ToList();

        return result;
    }

    private int FindBoundaryIndex(List<ColumnBoundary> boundaries, PositionedCharacter character, float tolerance)
    {
        var center = character.CenterX;

        var candidateIndex = -1;
        var candidateScore = float.MaxValue;

        for (int i = 0; i < boundaries.Count; i++)
        {
            var boundary = boundaries[i];
            if (center >= boundary.Start - tolerance && center <= boundary.End + tolerance)
            {
                var boundaryCenter = boundary.Center;
                var distance = Math.Abs(center - boundaryCenter);

                if (center >= boundary.Start && center <= boundary.End)
                {
                    distance *= 0.5f;
                }

                if (distance < candidateScore)
                {
                    candidateScore = distance;
                    candidateIndex = i;
                }
            }
        }

        if (candidateIndex != -1)
        {
            return candidateIndex;
        }

        var closestIndex = -1;
        var closestDistance = float.MaxValue;

        for (int i = 0; i < boundaries.Count; i++)
        {
            var boundary = boundaries[i];
            var distance = Math.Min(Math.Abs(center - boundary.Start), Math.Abs(center - boundary.End));

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        if (closestDistance <= tolerance * 2)
        {
            return closestIndex;
        }

        return -1;
    }

    /// <summary>
    /// Creates a PdfTable object from detected rows
    /// </summary>
    private PdfTable CreateTableFromRows(List<string[]> rows, int pageNum, int startLine, int columnCount)
    {
        var normalizedRows = rows.Select(row => NormalizeRow(row, columnCount)).ToList();
        var headers = normalizedRows.Count > 0 ? normalizedRows[0] : new string[columnCount];
        var dataRows = normalizedRows.Count > 1 ? normalizedRows.Skip(1).ToList() : new List<string[]>();

        return new PdfTable
        {
            PageNumber = pageNum,
            StartLine = startLine,
            Headers = headers.ToList(),
            Rows = dataRows,
            ColumnCount = columnCount,
            RowCount = dataRows.Count
        };
    }

    private string[] NormalizeRow(IReadOnlyList<string> columns, int columnCount)
    {
        var normalized = new string[columnCount];

        for (int i = 0; i < columnCount; i++)
        {
            normalized[i] = i < columns.Count ? columns[i].Trim() : string.Empty;
        }

        return normalized;
    }

    private List<ColumnBoundary> DetectColumnBoundaries(PositionedTextLine line)
    {
        var boundaries = new List<ColumnBoundary>();

        if (line.Characters.Count == 0)
        {
            return boundaries;
        }

        var threshold = CalculateGapThreshold(line);
        var overlapTolerance = CalculateOverlapTolerance(line);
        ColumnBoundary? current = null;

        foreach (var character in line.Characters)
        {
            if (current == null)
            {
                current = new ColumnBoundary
                {
                    Start = character.X,
                    End = character.EndX
                };
                continue;
            }

            var gap = character.X - current.End;

            if (gap > threshold)
            {
                boundaries.Add(current);
                current = new ColumnBoundary
                {
                    Start = character.X,
                    End = character.EndX
                };
            }
            else if (gap < -overlapTolerance)
            {
                var splitPoint = (current.End + character.X) / 2f;
                current.End = Math.Max(current.Start, splitPoint);
                boundaries.Add(current);
                current = new ColumnBoundary
                {
                    Start = Math.Min(character.X, splitPoint),
                    End = character.EndX
                };
            }
            else
            {
                current.Start = Math.Min(current.Start, character.X);
                current.End = Math.Max(current.End, character.EndX);
            }
        }

        if (current != null)
        {
            boundaries.Add(current);
        }

        ApplyPadding(boundaries, Math.Max(2f, threshold / 3f));
        EnsureNonOverlappingBoundaries(boundaries);

        return boundaries;
    }

    private static void ApplyPadding(List<ColumnBoundary> boundaries, float padding)
    {
        if (padding <= 0f)
        {
            return;
        }

        foreach (var boundary in boundaries)
        {
            boundary.Start -= padding;
            boundary.End += padding;
        }
    }

    private static void EnsureNonOverlappingBoundaries(List<ColumnBoundary> boundaries)
    {
        if (boundaries.Count < 2)
        {
            return;
        }

        boundaries.Sort((a, b) => a.Start.CompareTo(b.Start));

        for (int i = 1; i < boundaries.Count; i++)
        {
            var previous = boundaries[i - 1];
            var current = boundaries[i];

            if (previous.End > current.Start)
            {
                var midpoint = (previous.End + current.Start) / 2f;
                previous.End = midpoint;
                current.Start = midpoint;
            }

            if (current.Start > current.End)
            {
                current.Start = current.End;
            }

            if (previous.Start > previous.End)
            {
                previous.Start = previous.End;
            }
        }
    }

    private float CalculateOverlapTolerance(PositionedTextLine line)
    {
        var averageWidth = line.GetAverageCharacterWidth();

        if (averageWidth <= 0)
        {
            return 1.5f;
        }

        return Math.Max(1.5f, averageWidth * 0.6f);
    }

    private float CalculateGapThreshold(PositionedTextLine line)
    {
        if (line.Characters.Count < 2)
        {
            return Math.Max(6f, line.GetAverageCharacterWidth() * 2.5f);
        }

        var gaps = new List<float>();

        for (int i = 1; i < line.Characters.Count; i++)
        {
            var previous = line.Characters[i - 1];
            var current = line.Characters[i];
            var gap = current.X - previous.EndX;

            if (gap > 0)
            {
                gaps.Add(gap);
            }
        }

        var averageWidth = line.GetAverageCharacterWidth();
        var baseline = Math.Max(6f, averageWidth > 0 ? averageWidth * 2f : 6f);

        if (gaps.Count == 0)
        {
            return baseline;
        }

        gaps.Sort();

        var smallGapThreshold = baseline * 1.75f;
        var smallGaps = gaps.Where(gap => gap <= smallGapThreshold).ToList();

        float threshold;

        if (smallGaps.Count >= 2)
        {
            var typicalGap = smallGaps.Average();
            threshold = typicalGap * 1.5f;
        }
        else if (smallGaps.Count == 1 && gaps.Count > 1)
        {
            var median = gaps[gaps.Count / 2];
            threshold = median * 0.9f;
        }
        else
        {
            var candidate = gaps.Min() * 0.9f;
            threshold = candidate;
        }

        var upperBound = baseline * 4f;
        threshold = Math.Min(threshold, upperBound);
        threshold = Math.Max(threshold, baseline);

        return threshold;
    }

    private List<PositionedTextLine> ExtractPageLines(PdfPage page)
    {
        var strategy = new PositionedTextExtractionStrategy();
        var processor = new PdfCanvasProcessor(strategy);
        processor.ProcessPageContent(page);
        return strategy.GetLines();
    }

    private sealed class ColumnSplitResult
    {
        public List<string> Columns { get; set; } = new();
        public List<ColumnBoundary> Boundaries { get; set; } = new();
    }

    private sealed class ColumnBoundary
    {
        public float Start { get; set; }
        public float End { get; set; }

        public float Center => (Start + End) / 2f;

        public ColumnBoundary Clone() => new()
        {
            Start = Start,
            End = End
        };
    }

    private sealed class PositionedCharacter
    {
        public PositionedCharacter(string text, float x, float y, float width)
        {
            Text = text;
            X = x;
            Y = y;
            Width = width;
        }

        public string Text { get; }
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float EndX => X + Width;
        public float CenterX => X + Width / 2f;
    }

    private sealed class PositionedTextLine
    {
        private readonly List<PositionedCharacter> _characters = new();

        public PositionedTextLine(float y)
        {
            Y = y;
        }

        public float Y { get; }

        public IReadOnlyList<PositionedCharacter> Characters => _characters;

        public void AddCharacter(PositionedCharacter character) => _characters.Add(character);

        public void SortCharacters() => _characters.Sort((a, b) => a.X.CompareTo(b.X));

        public string GetText() => string.Concat(_characters.Select(c => c.Text));

        public string GetTrimmedText() => GetText().Trim();

        public float GetAverageCharacterWidth()
        {
            var widths = _characters
                .Select(c => c.Width)
                .Where(w => w > 0)
                .ToList();

            if (widths.Count == 0)
            {
                return 0f;
            }

            return widths.Sum() / widths.Count;
        }
    }

    private sealed class PositionedTextExtractionStrategy : IEventListener
    {
        private readonly List<PositionedCharacter> _characters = new();

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type != EventType.RENDER_TEXT)
            {
                return;
            }

            if (data is TextRenderInfo renderInfo)
            {
                foreach (var characterInfo in renderInfo.GetCharacterRenderInfos())
                {
                    var text = characterInfo.GetText();

                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    var baseline = characterInfo.GetBaseline();
                    var startPoint = baseline.GetStartPoint();
                    var endPoint = baseline.GetEndPoint();
                    var x = (float)startPoint.Get(Vector.I1);
                    var y = (float)startPoint.Get(Vector.I2);
                    var width = (float)Math.Abs(endPoint.Get(Vector.I1) - startPoint.Get(Vector.I1));

                    if (width <= 0)
                    {
                        width = characterInfo.GetSingleSpaceWidth();
                    }

                    _characters.Add(new PositionedCharacter(text, x, y, width));
                }
            }
        }

        public ICollection<EventType> GetSupportedEvents() => new HashSet<EventType> { EventType.RENDER_TEXT };

        public List<PositionedTextLine> GetLines()
        {
            const float yTolerance = 2.5f;

            var orderedCharacters = _characters
                .OrderByDescending(c => c.Y)
                .ToList();

            var lines = new List<PositionedTextLine>();

            foreach (var character in orderedCharacters)
            {
                var line = lines.FirstOrDefault(existing => Math.Abs(existing.Y - character.Y) <= yTolerance);

                if (line == null)
                {
                    line = new PositionedTextLine(character.Y);
                    lines.Add(line);
                }

                line.AddCharacter(character);
            }

            foreach (var line in lines)
            {
                line.SortCharacters();
            }

            lines.Sort((a, b) => b.Y.CompareTo(a.Y));

            return lines;
        }
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
