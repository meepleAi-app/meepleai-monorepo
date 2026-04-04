namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

internal interface IChunkTranslationService
{
    Task<List<TranslatedChunk>> TranslateChunksAsync(
        IReadOnlyList<string> chunks,
        string sourceLanguage,
        string targetLanguage = "en",
        CancellationToken ct = default);
}

internal record TranslatedChunk(
    int OriginalIndex,
    string OriginalText,
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage);
