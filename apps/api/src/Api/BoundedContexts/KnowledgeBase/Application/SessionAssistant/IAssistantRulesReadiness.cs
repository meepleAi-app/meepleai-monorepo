namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Se il gioco della sessione ha un regolamento che <c>cerca_regole</c> può consultare (#4729).
/// Senza, il turno non si ferma: il modello riceve una lista vuota e risponde che non ha trovato niente,
/// e l'host non ha modo di sapere che il problema è un regolamento mai caricato. Il pannello lo dice prima.
/// </summary>
internal interface IAssistantRulesReadiness
{
    /// <summary>
    /// <c>true</c> se il gioco della sessione ha un regolamento pronto, <c>false</c> se no (nessun PDF, o ancora in
    /// lavorazione, o fallito), <c>null</c> se non si può dire: la sessione non ha un gioco, o il gioco è privato.
    /// Lancia <see cref="Api.Middleware.Exceptions.NotFoundException"/> se la sessione non esiste.
    /// </summary>
    Task<bool?> HasIndexedRulesAsync(Guid liveSessionId, CancellationToken cancellationToken);
}
