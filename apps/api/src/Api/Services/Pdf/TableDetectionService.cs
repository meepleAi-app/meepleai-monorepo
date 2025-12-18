using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace Api.Services.Pdf;

/// <summary>
/// Service responsible for identifying table regions in PDF pages
/// </summary>
internal class TableDetectionService : ITableDetectionService
{
    private readonly ITableCellParser _cellParser;

    public TableDetectionService(ITableCellParser cellParser)
    {
        _cellParser = cellParser;
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
        if (lines.Count == 0)
        {
            return new List<PdfTable>();
        }

        var context = new DetectionContext(pageNum);
        int i = 0;

        while (i < lines.Count)
        {
            var line = lines[i];
            var shouldAdvance = ProcessLine(line, context);

            if (shouldAdvance)
            {
                i++;
            }
        }

        FinalizeCurrentTable(context);

        return context.Tables;
    }

    private bool ProcessLine(PositionedTextLine line, DetectionContext context)
    {
        var trimmedText = line.GetTrimmedText();

        if (string.IsNullOrWhiteSpace(trimmedText))
        {
            HandleBlankLine(context);
            return true; // Advance to next line
        }

        if (context.CurrentBoundaries != null && context.LastLineWasBlank)
        {
            if (IsTableInterrupted(line, context))
            {
                FinalizeCurrentTable(context);
                context.LastLineWasBlank = false;
                return false; // Don't increment i - reprocess this line
            }
        }

        context.LastLineWasBlank = false;

        if (context.CurrentBoundaries == null)
        {
            TryStartTable(line, context, context.CurrentLineIndex);
        }
        else
        {
            if (ShouldTerminateTable(line, context))
            {
                FinalizeCurrentTable(context);
                return false; // Don't increment i - reprocess this line
            }

            ProcessTableContent(line, context);
        }

        context.CurrentLineIndex++;
        return true;
    }

    private void HandleBlankLine(DetectionContext context)
    {
        if (context.CurrentBoundaries != null && context.CurrentBoundaries.Count > 0 && context.CurrentRows.Count > 0)
        {
            context.CurrentRows.Add(Enumerable.Repeat(string.Empty, context.CurrentBoundaries.Count).ToArray());
        }
        else
        {
            FinalizeCurrentTable(context);
        }

        context.LastLineWasBlank = true;
        context.CurrentLineIndex++;
    }

    private bool IsTableInterrupted(PositionedTextLine line, DetectionContext context)
    {
        var previewSplit = _cellParser.SplitIntoColumns(line, null);
        var previewColumnCount = Math.Max(previewSplit.Columns.Count, previewSplit.Boundaries.Count);
        var hasBlankRowSentinel = context.CurrentRows.Count > 0 && context.CurrentRows[^1].All(string.IsNullOrWhiteSpace);

        // Count non-empty columns to avoid false positives (e.g. page numbers)
        var previewNonEmptyColumns = previewSplit.Columns.Count(value => !string.IsNullOrWhiteSpace(value));

        // If we have a running table, strict requirements for continuing after a break
        var requiredPreviewColumns = context.CurrentBoundaries!.Count > 0
            ? Math.Max(2, (context.CurrentBoundaries.Count + 1) / 2)
            : 0;

        if (previewColumnCount > 0 && previewColumnCount < context.CurrentBoundaries.Count)
        {
            return true;
        }

        if (hasBlankRowSentinel && context.CurrentRows.Count > 1 && previewNonEmptyColumns >= requiredPreviewColumns)
        {
            return true;
        }

        return false;
    }

    private void TryStartTable(PositionedTextLine line, DetectionContext context, int lineIndex)
    {
        var split = _cellParser.SplitIntoColumns(line, null);

        if (split.Columns.Count >= 2)
        {
            context.CurrentBoundaries = split.Boundaries;
            context.TableStartLine = lineIndex;
            context.CurrentRows.Add(NormalizeRow(split.Columns, context.CurrentBoundaries.Count));
        }
        else
        {
            FinalizeCurrentTable(context);
        }
    }

    private bool ShouldTerminateTable(PositionedTextLine line, DetectionContext context)
    {
        if (context.CurrentBoundaries!.Count > 0 && line.Characters.Count > 0)
        {
            var rowStart = line.Characters.Min(c => c.X);
            var rowEnd = line.Characters.Max(c => c.EndX);
            var firstBoundary = context.CurrentBoundaries[0];
            var lastBoundary = context.CurrentBoundaries[^1];
            var driftTolerance = Math.Max(8f, line.GetAverageCharacterWidth() * 3f);

            var extendsLeft = rowStart < firstBoundary.Start - driftTolerance;
            var extendsRight = rowEnd > lastBoundary.End + driftTolerance;

            if (extendsLeft || extendsRight)
            {
                return true;
            }
        }
        return false;
    }

    private void ProcessTableContent(PositionedTextLine line, DetectionContext context)
    {
        var split = _cellParser.SplitIntoColumns(line, context.CurrentBoundaries);
        var boundariesForRow = split.Boundaries;
        var columnCount = boundariesForRow.Count;

        if (columnCount > context.CurrentBoundaries!.Count)
        {
            // Expand existing rows if column count increased
            for (int r = 0; r < context.CurrentRows.Count; r++)
            {
                context.CurrentRows[r] = NormalizeRow(context.CurrentRows[r], columnCount);
            }
        }

        context.CurrentBoundaries = boundariesForRow;

        var normalizedRow = NormalizeRow(split.Columns, columnCount);
        var hasContent = normalizedRow.Any(value => !string.IsNullOrWhiteSpace(value));

        if (hasContent)
        {
            context.CurrentRows.Add(normalizedRow);
        }
        else
        {
            FinalizeCurrentTable(context);
        }
    }

    private void FinalizeCurrentTable(DetectionContext context)
    {
        TrimTrailingEmptyRows(context);

        if (context.CurrentRows.Count > 1 && context.CurrentBoundaries != null)
        {
            context.Tables.Add(CreateTableFromRows(
                context.CurrentRows,
                context.PageNum,
                context.TableStartLine >= 0 ? context.TableStartLine : 0,
                context.CurrentBoundaries.Count));
        }

        context.CurrentRows.Clear();
        context.CurrentBoundaries = null;
        context.TableStartLine = -1;
        context.LastLineWasBlank = false;
    }

    private static void TrimTrailingEmptyRows(DetectionContext context)
    {
        while (context.CurrentRows.Count > 0 && context.CurrentRows[^1].All(value => string.IsNullOrWhiteSpace(value)))
        {
            context.CurrentRows.RemoveAt(context.CurrentRows.Count - 1);
        }
    }

    private class DetectionContext
    {
        public DetectionContext(int pageNum)
        {
            PageNum = pageNum;
        }

        public int PageNum { get; }
        public List<string[]> CurrentRows { get; } = new();
        public IList<ColumnBoundary>? CurrentBoundaries { get; set; }
        public int TableStartLine { get; set; } = -1;
        public bool LastLineWasBlank { get; set; }
        public List<PdfTable> Tables { get; } = new();
        public int CurrentLineIndex { get; set; }
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

    private string[] NormalizeRow(IList<string> columns, int columnCount)
    {
        var normalized = new string[columnCount];

        for (int i = 0; i < columnCount; i++)
        {
            normalized[i] = i < columns.Count ? columns[i].Trim() : string.Empty;
        }

        return normalized;
    }
}
