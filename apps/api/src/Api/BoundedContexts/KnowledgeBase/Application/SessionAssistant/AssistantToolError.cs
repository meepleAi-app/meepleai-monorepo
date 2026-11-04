namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Un errore che il modello deve leggere e risolvere chiedendo all'host (spec §6.3): argomenti non
/// validi, giocatore ambiguo, dimensione mancante, partita non modificabile.
/// </summary>
internal sealed record AssistantToolError(string Error, string Message, IReadOnlyList<string>? Candidates = null);
