namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Hardcoded IT/EN fallback messages for alpha.
/// Future: swap for resource-based localization (#448 C4).
/// </summary>
internal sealed class DefaultCopyrightFallbackMessageProvider : ICopyrightFallbackMessageProvider
{
    private const string ItalianMessage =
        "Non posso mostrare il contenuto in forma letterale perché proviene da materiale protetto da copyright. " +
        "Prova a riformulare la tua domanda per ottenere una spiegazione sintetizzata delle regole.";

    private const string EnglishMessage =
        "I cannot display the content verbatim because it comes from copyright-protected material. " +
        "Try rephrasing your question to get a synthesized explanation of the rules.";

    public string GetMessage(string language) => language switch
    {
        "it" => ItalianMessage,
        _    => EnglishMessage
    };
}
