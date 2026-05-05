namespace Api.Infrastructure.Translation;

internal sealed record TranslationResult(
    bool Success,
    string TranslatedText,
    string SourceLanguage,
    string TargetLanguage,
    decimal EstimatedCostUsd,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static TranslationResult CreateSuccess(
        string translated, string src, string tgt, decimal cost) =>
        new(true, translated, src, tgt, cost);

    public static TranslationResult CreateFailure(string src, string tgt, string errorCode, string msg) =>
        new(false, string.Empty, src, tgt, 0m, errorCode, msg);
}
