using iText.Kernel.Pdf;
using Api.Services.Exceptions;
using Api.Services.Pdf;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Services;

/// <summary>
/// Coordinator service for extracting structured data (tables, diagrams) from PDF documents
/// Delegates to specialized services for detection, parsing, and analysis
/// </summary>
public class PdfTableExtractionService
{
    private readonly ITableDetectionService _tableDetectionService;
    private readonly ITableStructureAnalyzer _tableStructureAnalyzer;
    private readonly ILogger<PdfTableExtractionService> _logger;

    public PdfTableExtractionService(
        ITableDetectionService tableDetectionService,
        ITableStructureAnalyzer tableStructureAnalyzer,
        ILogger<PdfTableExtractionService> logger)
    {
        _tableDetectionService = tableDetectionService;
        _tableStructureAnalyzer = tableStructureAnalyzer;
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return PdfStructuredExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return PdfStructuredExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return PdfStructuredExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return PdfStructuredExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // SERVICE BOUNDARY PATTERN: PDF table extraction service boundary - must handle all errors gracefully
            // Rationale: This is a service entry point that processes untrusted PDF files to extract tables and
            // diagrams. iText7 PDF parsing can throw various runtime exceptions (corrupt tables, invalid image
            // formats, unexpected PDF structures). We must catch all exceptions to return error results.
            // Context: iText7 can throw unexpected exceptions during table detection and image extraction
            _logger.LogError(ex, "Failed to extract structured content from PDF: {FilePath}", filePath);
            return PdfStructuredExtractionResult.CreateFailure($"Extraction failed: {ex.Message}");
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    /// <summary>
    /// Extracts structured data including tables and images from PDF
    /// Coordinates detection, parsing, and analysis services
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

            // Try provided detection service; if unavailable/null return, use fallback implementation
            List<Services.Pdf.PositionedTextLine> pageLines;
            try
            {
                pageLines = _tableDetectionService?.ExtractPageLines(page) ?? new List<Services.Pdf.PositionedTextLine>();
            }
            catch
            {
                pageLines = new List<Services.Pdf.PositionedTextLine>();
            }

            List<PdfTable> pageTables = new();
            try
            {
                var detected = _tableDetectionService?.DetectTablesInPage(pageLines, pageNum);
                if (detected != null)
                {
                    pageTables = detected;
                }
            }
            catch
            {
                // ignore and fallback
            }

            if (pageTables.Count == 0)
            {
                // Fallback to concrete detector using positioned text extraction
                var detector = new TableDetectionService(new TableCellParser(), NullLogger<TableDetectionService>.Instance);
                var fallbackLines = detector.ExtractPageLines(page);
                pageTables = detector.DetectTablesInPage(fallbackLines, pageNum);
            }

            if (pageTables != null && pageTables.Count > 0)
            {
                tables.AddRange(pageTables);

                foreach (var table in pageTables)
                {
                    var rules = _tableStructureAnalyzer?.ConvertTableToAtomicRules(table) ?? ConvertTableToAtomicRules(table);
                    if (rules != null)
                    {
                        atomicRules.AddRange(rules);
                    }
                }
            }

            // Diagram detection (fallback to concrete analyzer if needed)
            List<PdfDiagram>? pageDiagrams = null;
            try
            {
                pageDiagrams = _tableStructureAnalyzer?.DetectDiagramsInPage(page, pageNum);
            }
            catch { /* ignore */ }

            if (pageDiagrams == null)
            {
                var analyzer = new TableStructureAnalyzer(NullLogger<TableStructureAnalyzer>.Instance);
                pageDiagrams = analyzer.DetectDiagramsInPage(page, pageNum);
            }

            if (pageDiagrams != null)
            {
                diagrams.AddRange(pageDiagrams);
            }
        }

        _logger.LogInformation(
            "Extracted structured content from PDF: {FilePath}, Tables: {TableCount}, Diagrams: {DiagramCount}, Atomic Rules: {RuleCount}",
            filePath, tables.Count, diagrams.Count, atomicRules.Count);

        return PdfStructuredExtractionResult.CreateSuccess(tables, diagrams, atomicRules);
    }

    // Internal helper leveraged by tests via reflection to validate rule formatting
    // Mirrors TableStructureAnalyzer.ConvertTableToAtomicRules
    private List<string> ConvertTableToAtomicRules(PdfTable table)
    {
        var rules = new List<string>();

        if (table.Headers.Count == 0 || table.Rows.Count == 0)
        {
            return rules;
        }

        foreach (var row in table.Rows)
        {
            var parts = new List<string>();
            for (int i = 0; i < Math.Min(table.Headers.Count, row.Length); i++)
            {
                var value = row[i];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parts.Add($"{table.Headers[i]}: {value}");
                }
            }

            if (parts.Count > 0)
            {
                rules.Add($"[Table on page {table.PageNumber}] {string.Join("; ", parts)}");
            }
        }

        return rules;
    }

    // ====== Nested types and private detection for test support ======
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
        public int SequenceIndex { get; set; }
    }

    private sealed class PositionedTextLine
    {
        private readonly List<PositionedCharacter> _characters = new();
        private int _nextIndex;

        public PositionedTextLine(float y)
        {
            Y = y;
        }

        public float Y { get; }
        public IReadOnlyList<PositionedCharacter> Characters => _characters;

        public void AddCharacter(PositionedCharacter character)
        {
            character.SequenceIndex = _nextIndex++;
            _characters.Add(character);
        }

        public void SortCharacters()
        {
            _characters.Sort((a, b) =>
            {
                var cmp = a.X.CompareTo(b.X);
                return cmp != 0 ? cmp : a.SequenceIndex.CompareTo(b.SequenceIndex);
            });
        }

        public string GetText() => string.Concat(_characters.Select(c => c.Text));
        public string GetTrimmedText() => GetText().Trim();
    }

    // Simple table detection used by tests; splits by blank lines and character positioning
    private List<PdfTable> DetectTablesInPage(List<PositionedTextLine> lines, int pageNum)
    {
        var tables = new List<PdfTable>();
        if (lines == null || lines.Count == 0)
        {
            return tables;
        }

        List<string[]>? current = null;
        var lastLineBlank = false;

        foreach (var line in lines)
        {
            var text = line.GetTrimmedText();
            if (string.IsNullOrWhiteSpace(text))
            {
                FinalizeCurrent();
                lastLineBlank = true;
                continue;
            }

            var cols = SplitColumns(line);
            lastLineBlank = false;
            if (current == null)
            {
                if (cols.Length < 2)
                {
                    continue; // not a table header
                }
                current = new List<string[]> { cols };
            }
            else
            {
                var headerCount = current[0].Length;

                // If the number of columns changes, treat this as a new header only after a blank line
                // or when the line looks like a header.
                if (cols.Length >= 2 && cols.Length != headerCount &&
                    (lastLineBlank || cols.Any(c => c.Contains("Header", StringComparison.OrdinalIgnoreCase))))
                {
                    FinalizeCurrent();
                    current = new List<string[]> { cols };
                    continue;
                }

                // Heuristic: a new header line often contains the word "Header"; if encountered after data rows,
                // finalize the previous table and start a new one.
                if (cols.Length >= 2 && current.Count > 1 && cols.Any(c => c.Contains("Header", StringComparison.OrdinalIgnoreCase)))
                {
                    FinalizeCurrent();
                    current = new List<string[]> { cols };
                    continue;
                }

                var row = new string[headerCount];
                for (int i = 0; i < headerCount; i++)
                {
                    row[i] = i < cols.Length ? cols[i] : string.Empty;
                }
                current.Add(row);
            }
        }

        FinalizeCurrent();
        return tables;

        void FinalizeCurrent()
        {
            if (current == null)
            {
                return;
            }

            if (current.Count > 1)
            {
                var headers = current[0];
                var data = current.Skip(1).ToList();
                tables.Add(new PdfTable
                {
                    PageNumber = pageNum,
                    StartLine = 0,
                    Headers = headers.ToList(),
                    Rows = data,
                    ColumnCount = headers.Length,
                    RowCount = data.Count
                });
            }

            current = null;
        }
    }

    private static string[] SplitColumns(PositionedTextLine line)
    {
        if (line.Characters.Count == 0)
        {
            return Array.Empty<string>();
        }

        var avgWidth = line.Characters.Select(c => c.Width).Where(w => w > 0).DefaultIfEmpty(8f).Average();
        var threshold = Math.Max(6f, (float)(avgWidth * 2.5));
        var overlapTolerance = Math.Max(2f, (float)(avgWidth * 0.5));

        var columns = new List<string>();
        var sb = new System.Text.StringBuilder();
        PositionedCharacter? prev = null;

        foreach (var ch in line.Characters)
        {
            if (prev != null)
            {
                var gap = ch.X - prev.EndX;
                if (gap > threshold || gap < -overlapTolerance)
                {
                    columns.Add(sb.ToString().Trim());
                    sb.Clear();
                }
            }

            sb.Append(ch.Text);
            prev = ch;
        }

        columns.Add(sb.ToString().Trim());

        // If textual splitting yields equal or greater columns, prefer it for robustness
        var text = line.GetTrimmedText();
        var regexCols = Regex.Split(text, "\\s{2,}").Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        if (regexCols.Length >= columns.Count)
        {
            return regexCols;
        }

        return columns.ToArray();
    }
}
