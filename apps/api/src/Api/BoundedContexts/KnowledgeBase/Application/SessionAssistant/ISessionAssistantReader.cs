namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>
/// La porta di sola lettura degli strumenti (spec §3.2). La sessione è legata all'istanza dal server:
/// gli strumenti non ricevono un id di sessione e il modello non può sceglierne un'altra.
/// </summary>
internal interface ISessionAssistantReader
{
    Task<AssistantSessionState> GetStateAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AssistantRulePassage>> SearchRulesAsync(string question, CancellationToken cancellationToken);
}
