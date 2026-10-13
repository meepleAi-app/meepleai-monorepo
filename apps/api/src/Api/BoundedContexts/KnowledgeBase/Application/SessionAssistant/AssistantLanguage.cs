namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>La lingua di una sessione, come arriva dal client: «it», «en», «en-US».</summary>
internal static class AssistantLanguage
{
    /// <summary>
    /// Vero per «en» e per le sue varianti regionali; ogni altra lingua usa l'italiano. Un confronto esatto
    /// su «en» dava il prompt italiano a una sessione «en-US» (#4256).
    /// </summary>
    public static bool IsEnglish(string? language) =>
        language is not null
        && language.StartsWith("en", StringComparison.OrdinalIgnoreCase)
        && (language.Length == 2 || language[2] is '-' or '_');
}
