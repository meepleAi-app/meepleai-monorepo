using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to extract a specific page from an uploaded PDF as a JPEG image.
/// Used in the import wizard step 2 cover image picker (Tab: "Estrai dal PDF").
/// </summary>
/// <param name="PdfDocumentId">ID of the uploaded PdfDocument</param>
/// <param name="PageNumber">1-based page number to extract</param>
public record GetPdfPageImageQuery(
    Guid PdfDocumentId,
    int PageNumber
) : IQuery<byte[]>;
