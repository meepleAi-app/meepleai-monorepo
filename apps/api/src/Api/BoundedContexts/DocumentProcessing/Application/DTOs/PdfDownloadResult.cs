namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of PDF download query containing file stream and metadata.
/// </summary>
/// <param name="FileStream">The file stream to return to client</param>
/// <param name="FileName">Original file name for download</param>
/// <param name="ContentType">MIME type of the file</param>
/// <param name="PdfId">The PDF document ID</param>
internal record PdfDownloadResult(
    Stream FileStream,
    string FileName,
    string ContentType,
    Guid PdfId
);
