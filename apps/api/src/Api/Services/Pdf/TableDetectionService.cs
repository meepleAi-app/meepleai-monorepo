using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace Api.Services.Pdf;

/// <summary>
/// Service responsible for identifying table regions in PDF pages
/// </summary>
public class TableDetectionService : ITableDetectionService
{
    private readonly ITableCellParser _cellParser;
    private readonly ILogger<TableDetectionService> _logger;

    public TableDetectionService(
        ITableCellParser cellParser,
        ILogger<TableDetectionService> logger)
    {
        _cellParser = cellParser;
        _logger = logger;
    }

    public List<PositionedTextLine> ExtractPageLines(PdfPage page)
    {
        var strategy = new PositionedTextExtractionStrategy();
        var processor = new PdfCanvasProcessor(strategy);
        processor.ProcessPageContent(page);
        return strategy.GetLines();
    }

    public List<PdfTable> DetectTablesInPage(List<PositionedTextLine> lines, int pageNum)
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
                var previewSplit = _cellParser.SplitIntoColumns(line, null);
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
                var split = _cellParser.SplitIntoColumns(line, null);

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

                var split = _cellParser.SplitIntoColumns(line, currentBoundaries);
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
}
