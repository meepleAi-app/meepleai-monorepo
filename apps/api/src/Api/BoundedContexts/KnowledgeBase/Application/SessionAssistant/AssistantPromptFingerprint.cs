namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// L'impronta (SHA-256, esadecimale) dei due prompt usati in una valutazione. La richiesta può mandare un
/// prompt alternativo, ma un'immagine che non conosce quel campo lo scarta in silenzio e misura il
/// predefinito: l'impronta nel report dice quale testo è stato usato davvero, e chi ha mandato il prompt la
/// confronta con la propria.
/// </summary>
internal sealed record AssistantPromptFingerprint(string Italian, string English);
