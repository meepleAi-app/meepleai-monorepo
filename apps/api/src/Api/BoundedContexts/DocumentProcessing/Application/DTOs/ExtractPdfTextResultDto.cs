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
    public static ExtractPdfTextResultDto CreateSuccess(int characterCount, int pageCount)
        => new(true, null, characterCount, pageCount, "Ready");

    public static ExtractPdfTextResultDto CreateFailure(string errorMessage)
        => new(false, errorMessage, null, null, "Failed");
}
