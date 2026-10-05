namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Data Transfer Object for Agent aggregate.
/// </summary>
internal record AgentDto(
    Guid Id,
    string Name,
    string Type,
    string StrategyName,
    IReadOnlyDictionary<string, object> StrategyParameters,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastInvokedAt,
    int InvocationCount,
    bool IsRecentlyUsed,
    bool IsIdle,
    // 🔴 Issue #4081: se questo agente e' definito dal sistema e non dall'utente.
    //
    // Il campo esisteva sull'entita' (`AgentDefinition.IsSystemDefined`, colonna
    // `knowledge_base.agent_definitions.is_system_defined`) e si perdeva nel mapping, quindi il
    // client non aveva nulla su cui distinguere. Risultato: la libreria personale mostrava
    // l'agente di sistema `Rules Expert` come se fosse una risorsa dell'utente, su qualunque
    // account — incluso uno appena registrato con libreria vuota e zero agenti propri.
    //
    // ⚠️ E' obbligatorio e senza default di proposito. Con `= false` ogni sito di mapping che lo
    // dimenticasse dichiarerebbe «non di sistema» per un agente che lo e', cioe' esattamente il
    // difetto che questo campo esiste per chiudere.
    bool IsSystemDefined,
    Guid? GameId = null,
    string? GameName = null,
    Guid? CreatedByUserId = null
);
