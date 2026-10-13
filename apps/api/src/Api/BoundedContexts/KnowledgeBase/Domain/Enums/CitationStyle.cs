namespace Api.BoundedContexts.KnowledgeBase.Domain.Enums;

/// <summary>
/// Come la risposta dell'agente rimanda alle fonti (ADR-095 D1, sezione Generazione).
/// </summary>
public enum CitationStyle
{
    /// <summary>Riferimenti di pagina nel testo della risposta («p. 12»).</summary>
    PageReferencesInText = 0,

    /// <summary>Citazioni inviate come eventi strutturati a parte, senza riferimenti nel testo.</summary>
    StructuredEvents = 1
}
