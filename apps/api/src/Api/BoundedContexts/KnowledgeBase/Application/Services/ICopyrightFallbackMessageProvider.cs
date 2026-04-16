namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Provides localized fallback messages shown to the user when a copyright
/// leak is detected and the response is sanitized.
/// Extracted as a service to allow future resource-based localization (#448).
/// </summary>
internal interface ICopyrightFallbackMessageProvider
{
    /// <summary>
    /// Returns the fallback message for the given agent language.
    /// Unknown languages fall back to English.
    /// </summary>
    /// <param name="language">ISO 639-1 lowercase (e.g., "it", "en").</param>
    string GetMessage(string language);
}
