namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

/// <summary>
/// Represents a single text segment extracted from a gamebook photo.
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// </summary>
public sealed record GamebookSegment
{
    public int ParagraphNumber { get; }
    public string SourceText { get; }
    public string? BoundingBox { get; }

    private GamebookSegment(int paragraphNumber, string sourceText, string? boundingBox)
    {
        ParagraphNumber = paragraphNumber;
        SourceText = sourceText;
        BoundingBox = boundingBox;
    }

    public static GamebookSegment Create(int paragraphNumber, string sourceText, string? boundingBox)
    {
        if (paragraphNumber < 0)
            throw new ArgumentException("paragraphNumber must be >= 0", nameof(paragraphNumber));
        if (string.IsNullOrWhiteSpace(sourceText))
            throw new ArgumentException("sourceText required", nameof(sourceText));

        return new GamebookSegment(paragraphNumber, sourceText.Trim(), boundingBox);
    }
}
