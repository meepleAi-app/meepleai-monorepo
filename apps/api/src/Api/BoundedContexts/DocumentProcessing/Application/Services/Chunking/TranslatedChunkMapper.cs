using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;

/// <summary>
/// SP2: builds the English-translation chunk from an original chunk, preserving all hierarchy
/// fields (Heading/Level/ParentChunkId/ElementType) so translated chunks of non-EN rulebooks also
/// activate the role fast-path (resolves the #730 forward-wiring follow-up in PdfProcessingPipelineService).
/// </summary>
internal static class TranslatedChunkMapper
{
    public static DocumentChunkInput ForTranslation(DocumentChunkInput orig, string translatedText) =>
        orig with { Text = translatedText };
}
