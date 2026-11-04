#pragma warning disable MA0048 // File name must match type name - the directory port and what it returns
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// Chi possiede una sessione live e se l'assistente può risponderle (spec §6.2, passo 1).
/// <c>AcceptsQuestions</c> è vero finché la sessione non è conclusa: anche prima dell'avvio, quando gli
/// strumenti di proposta rifiutano e resta solo la lettura. <c>GameId</c> (#4729) è il gioco della sessione, che
/// la directory ha già in mano: il pannello non deve rileggere la sessione intera per saperlo.
/// </summary>
internal sealed record AssistantSessionAccess(Guid OwnerUserId, string Status, bool AcceptsQuestions, Guid? GameId = null);

/// <summary>
/// La porta verso la sessione live per il turno e per la conferma (spec §3.2). L'implementazione sta in
/// Infrastructure e passa dalle query di GameManagement: qui non compare nessun suo tipo.
/// </summary>
internal interface IAssistantSessionDirectory
{
    /// <summary>Lancia <see cref="Api.Middleware.Exceptions.NotFoundException"/> se la sessione non esiste.</summary>
    Task<AssistantSessionAccess> GetAccessAsync(Guid liveSessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Il lettore degli strumenti, legato a questa sessione dal server: il modello non può sceglierne
    /// un'altra. <paramref name="ownerUserId"/> serve alle house rules, che sono del proprietario.
    /// </summary>
    ISessionAssistantReader CreateReader(Guid liveSessionId, Guid ownerUserId);
}
