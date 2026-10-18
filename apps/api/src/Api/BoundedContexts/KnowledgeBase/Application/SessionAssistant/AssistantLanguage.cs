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

    /// <summary>
    /// Il codice a due lettere del recupero e del messaggio di copyright: «en» per l'inglese e le sue varianti,
    /// «it» per il resto, con la stessa regola che sceglie il prompt (#4411).
    /// </summary>
    public static string Code(string? language) => IsEnglish(language) ? "en" : "it";
}
