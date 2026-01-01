using iText.Kernel.Pdf;

namespace Api.Services.Pdf;

/// <summary>
/// Service responsible for identifying table regions in PDF pages
/// </summary>
internal interface ITableDetectionService
{
    /// <summary>
    /// Detects tables in a PDF page using positioned text analysis
    /// </summary>
    /// <param name="lines"></param>
    /// <param name="pageNum">Page number for metadata</param>
    /// <returns>List of detected tables</returns>
    List<PdfTable> DetectTablesInPage(List<PositionedTextLine> lines, int pageNum);

    /// <summary>
    /// Extracts positioned text lines from a PDF page
    /// </summary>
    /// <param name="page">PDF page to extract lines from</param>
    /// <returns>List of positioned text lines</returns>
    List<PositionedTextLine> ExtractPageLines(PdfPage page);
}
