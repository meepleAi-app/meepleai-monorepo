namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

public static class PdfStorageKey
{
    /// <summary>
    /// Bucket key for PDF storage. Uses pdf.Id to decouple from game lifecycle.
    /// Pre-migration PDFs stored under gameId bucket must be rebucket-ed by scripts/rebucket-pdfs.*
    /// </summary>
    public static string ForPdf(Guid pdfId) => pdfId.ToString("N");
}
