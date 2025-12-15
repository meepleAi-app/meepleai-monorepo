namespace Api.Services.Pdf;

/// <summary>
/// Service responsible for parsing individual table cells and columns
/// </summary>
internal interface ITableCellParser
{
    /// <summary>
    /// Splits a line into columns based on character positioning
    /// </summary>
    /// <param name="line">Line to split</param>
    /// <param name="existingBoundaries">Optional existing column boundaries to use</param>
    /// <returns>Column split result with boundaries and text</returns>
    ColumnSplitResult SplitIntoColumns(PositionedTextLine line, IList<ColumnBoundary>? existingBoundaries);

    /// <summary>
    /// Detects column layout from a text line
    /// </summary>
    /// <param name="line">Line to analyze</param>
    /// <returns>Detected column layout</returns>
    DetectedColumnLayout DetectColumnLayout(PositionedTextLine line);

    /// <summary>
    /// Calculates gap threshold for column detection
    /// </summary>
    /// <param name="line">Line to analyze</param>
    /// <returns>Gap threshold value</returns>
    float CalculateGapThreshold(PositionedTextLine line);

    /// <summary>
    /// Calculates overlap tolerance for column detection
    /// </summary>
    /// <param name="line">Line to analyze</param>
    /// <returns>Overlap tolerance value</returns>
    float CalculateOverlapTolerance(PositionedTextLine line);

    /// <summary>
    /// Finds the boundary index for a character
    /// </summary>
    /// <param name="boundaries">List of boundaries</param>
    /// <param name="character">Character to find boundary for</param>
    /// <param name="tolerance">Tolerance for matching</param>
    /// <returns>Boundary index or -1 if not found</returns>
    int FindBoundaryIndex(IList<ColumnBoundary> boundaries, PositionedCharacter character, float tolerance);
}
