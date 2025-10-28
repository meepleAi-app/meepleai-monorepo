using iText.Kernel.Pdf;

namespace Api.Services.Pdf;

/// <summary>
/// Service responsible for analyzing table structure and converting to atomic rules
/// </summary>
public interface ITableStructureAnalyzer
{
    /// <summary>
    /// Converts table rows to atomic rules (PDF-03 requirement)
    /// </summary>
    /// <param name="table">Table to convert</param>
    /// <returns>List of atomic rules</returns>
    List<string> ConvertTableToAtomicRules(PdfTable table);

    /// <summary>
    /// Detects diagrams and images in a PDF page (PDF-03 requirement)
    /// </summary>
    /// <param name="page">PDF page to analyze</param>
    /// <param name="pageNum">Page number</param>
    /// <returns>List of detected diagrams</returns>
    List<PdfDiagram> DetectDiagramsInPage(PdfPage page, int pageNum);
}
