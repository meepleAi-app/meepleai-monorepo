namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// La porta di sola lettura degli strumenti (spec §3.2). La sessione è legata all'istanza dal server:
/// gli strumenti non ricevono un id di sessione e il modello non può sceglierne un'altra. Legge lo stato della
/// partita; il regolamento è <see cref="IAssistantRuleSource"/> (#4411).
/// </summary>
internal interface ISessionAssistantReader
{
    Task<AssistantSessionState> GetStateAsync(CancellationToken cancellationToken);
}
