namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// I prompt di sistema dell'assistente, uno per lingua. Sono un dato: oggi vengono dal codice
/// (<see cref="SessionAssistantPrompts.Default"/>) o dalla richiesta di valutazione, nella fetta A2 dalla
/// sezione «Assistente in partita» del profilo. Così una variante si misura senza ricostruire l'immagine.
/// </summary>
internal sealed record AssistantPromptSet(string Italian, string English)
{
    public string For(string? language) => AssistantLanguage.IsEnglish(language) ? English : Italian;

    /// <summary>Sostituisce solo le lingue indicate: una stringa vuota non è un prompt.</summary>
    public AssistantPromptSet With(string? italian, string? english) => new(
        string.IsNullOrWhiteSpace(italian) ? Italian : italian,
        string.IsNullOrWhiteSpace(english) ? English : english);
}
