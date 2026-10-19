#pragma warning disable MA0048 // File name must match type name - record + interface + implementation in one file
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// #4435: what is known about a PDF's language when it is sent to an extractor — the three columns
/// of <c>pdf_documents</c>, unchanged. Deciding what to do with them is
/// <see cref="UnstructuredLanguageResolver"/>'s job.
/// </summary>
/// <param name="LanguageOverride">The admin's manual override (<c>language_override</c>), ISO 639-1.</param>
/// <param name="DetectedLanguage">The detected language (<c>Language</c>), ISO 639-1. On a row that was
/// never detected this is the entity default <c>"en"</c>, not a detection.</param>
/// <param name="DetectedConfidence">The detection confidence (<c>language_confidence</c>); null when the
/// language was never detected.</param>
internal sealed record DocumentLanguageHint(
    string? LanguageOverride,
    string? DetectedLanguage,
    double? DetectedConfidence);

/// <summary>
/// #4435: per-request holder for the language of the PDF being extracted. Same pattern as
/// <see cref="IExtractionStrategySelector"/>: registered <b>scoped</b>, set by
/// <c>PdfProcessingPipelineService</c> before it calls the extractor and read by
/// <see cref="UnstructuredPdfTextExtractor"/>, which is resolved in the same DI scope.
/// Null means "no document language available": the extractor sends the configured default set.
/// </summary>
internal interface IExtractionLanguageSelector
{
    DocumentLanguageHint? Current { get; set; }
}

/// <inheritdoc cref="IExtractionLanguageSelector"/>
internal sealed class ExtractionLanguageSelector : IExtractionLanguageSelector
{
    public DocumentLanguageHint? Current { get; set; }
}
