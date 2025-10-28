namespace Api.Services.Pdf;

/// <summary>
/// Positioned character in PDF page
/// </summary>
public sealed class PositionedCharacter
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
    public int SequenceIndex { get; internal set; } = -1;
}

/// <summary>
/// Positioned text line in PDF page
/// </summary>
public sealed class PositionedTextLine
{
    private readonly List<PositionedCharacter> _characters = new();
    private int _nextSequenceIndex;

    public PositionedTextLine(float y)
    {
        Y = y;
    }

    public float Y { get; }

    public IReadOnlyList<PositionedCharacter> Characters => _characters;

    public void AddCharacter(PositionedCharacter character)
    {
        character.SequenceIndex = _nextSequenceIndex++;
        _characters.Add(character);
    }

    public void SortCharacters() => _characters.Sort((a, b) =>
    {
        var xComparison = a.X.CompareTo(b.X);

        if (xComparison != 0)
        {
            return xComparison;
        }

        return a.SequenceIndex.CompareTo(b.SequenceIndex);
    });

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

/// <summary>
/// Column boundary definition
/// </summary>
public sealed class ColumnBoundary
{
    public float Start { get; set; }
    public float End { get; set; }
    public float ContentStart { get; set; }
    public float ContentEnd { get; set; }

    public float Center => (Start + End) / 2f;

    public static ColumnBoundary FromCharacter(PositionedCharacter character, float tolerance = 0f)
    {
        var start = tolerance > 0 ? character.X - tolerance : character.X;
        var end = tolerance > 0 ? character.EndX + tolerance : character.EndX;

        return new ColumnBoundary
        {
            Start = start,
            End = end,
            ContentStart = character.X,
            ContentEnd = character.EndX
        };
    }

    public void ExpandToInclude(PositionedCharacter character)
    {
        Start = Math.Min(Start, character.X);
        End = Math.Max(End, character.EndX);
        ContentStart = Math.Min(ContentStart, character.X);
        ContentEnd = Math.Max(ContentEnd, character.EndX);
    }

    public ColumnBoundary Clone() => new()
    {
        Start = Start,
        End = End,
        ContentStart = ContentStart,
        ContentEnd = ContentEnd
    };
}

/// <summary>
/// Result of column split operation
/// </summary>
public sealed class ColumnSplitResult
{
    public List<string> Columns { get; set; } = new();
    public List<ColumnBoundary> Boundaries { get; set; } = new();
}

/// <summary>
/// Detected column layout
/// </summary>
public sealed class DetectedColumnLayout
{
    public List<ColumnBoundary> Boundaries { get; } = new();
    public List<string> Columns { get; set; } = new();
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

/// <summary>
/// Extracted image information
/// </summary>
public class ExtractedImage
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
