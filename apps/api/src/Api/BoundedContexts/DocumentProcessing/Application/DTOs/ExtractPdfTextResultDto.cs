namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of PDF text extraction operation.
/// </summary>
internal record ExtractPdfTextResultDto(
    bool Success,
    string? ErrorMessage,
    int? CharacterCount,
    int? PageCount,
    string? ProcessingState)
{
    // B13: text-only extraction leaves the document in Extracting (only IndexPdfCommand reaches
    // Ready). Reporting "Ready" here falsely advertised the document as fully processed / searchable.
    public static ExtractPdfTextResultDto CreateSuccess(int characterCount, int pageCount)
        => new(true, null, characterCount, pageCount, nameof(Domain.Enums.PdfProcessingState.Extracting));

    public static ExtractPdfTextResultDto CreateFailure(string errorMessage)
        => new(false, errorMessage, null, null, "Failed");
}
