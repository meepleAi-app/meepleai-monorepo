namespace Api.SharedKernel.Constants;

/// <summary>
/// Page count category thresholds for PDF documents.
/// </summary>
internal static class PageCountCategories
{
    /// <summary>
    /// Minimum page count (1 page).
    /// </summary>
    public const int MinimumPageCount = 1;

    /// <summary>
    /// Threshold for "small" PDFs (10 pages or less).
    /// </summary>
    public const int SmallPdfThreshold = 10;

    /// <summary>
    /// Threshold for "medium" PDFs (11-100 pages).
    /// PDFs above this are considered "large".
    /// </summary>
    public const int MediumPdfThreshold = 100;
}
