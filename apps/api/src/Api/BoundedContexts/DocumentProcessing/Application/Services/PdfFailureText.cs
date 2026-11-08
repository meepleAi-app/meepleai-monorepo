using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// The text a failed PDF carries in <c>ProcessingError</c> and in the progress error: fixed, chosen by the error
/// category, never the exception's message (#4660).
/// </summary>
/// <remarks>
/// Those columns are served to users (pdf-status, the documents list, the knowledge-base status, the failure
/// notification, the progress stream), and an exception message is whatever its author put there: the key of the
/// object in the bucket, the host of the embedding service, a path, a fragment of the file. The detail belongs in
/// the log, where the failing code already writes it with the exception; what the user needs from the row is that
/// the PDF failed and the category (the same one <c>ErrorCategory</c> and <c>FailedAtState</c> record for whoever
/// investigates). Same shape as the text <c>IndexingError</c> has carried since #4609.
/// </remarks>
internal static class PdfFailureText
{
    /// <summary>
    /// Text extraction failed and no category is recorded for it (the extract command does not set one).
    /// </summary>
    public const string TextExtraction = "Text extraction failed";

    /// <summary>
    /// The extraction found pages and not one had text (#4437): the PDF is probably a scan, and the text extraction in use
    /// does not OCR it. Fixed like the others; which extractor read it and how many pages it had stay in the log.
    /// </summary>
    public const string NoExtractableText =
        "The PDF has no extractable text. It is probably a scan: the current text extraction does not perform OCR";

    /// <summary>The fixed text for a failure of this category.</summary>
    public static string For(ErrorCategory category) => $"Processing failed ({category})";
}
