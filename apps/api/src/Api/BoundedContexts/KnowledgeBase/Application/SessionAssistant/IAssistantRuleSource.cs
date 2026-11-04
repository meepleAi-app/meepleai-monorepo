using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

#pragma warning disable MA0048 // File name must match type name - the rule source port, what it returns and its scope
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Un passaggio del regolamento come lo vede il modello: il testo e la pagina.</summary>
internal sealed record AssistantRulePassage(string Text, int? Page);

/// <summary>
/// L'esito di una ricerca nel regolamento: i passaggi per il modello e le loro citazioni, con il tier già
/// risolto, per <c>FinalizeAsync</c>. <see cref="NotAccessible"/> se chi chiede non ha accesso alla KB.
/// </summary>
internal sealed record AssistantRuleSearch(
    IReadOnlyList<AssistantRulePassage> Passages, IReadOnlyList<ChunkCitation> Citations, bool Accessible = true)
{
    public static AssistantRuleSearch NotAccessible { get; } = new([], [], Accessible: false);
}

/// <summary>
/// <c>cerca_regole</c> (spec §4.2): il regolamento del gioco della sessione, letto per chi chiede. È legata al
/// turno dal server, come il lettore dello stato: il modello sceglie solo la domanda.
/// </summary>
internal interface IAssistantRuleSource
{
    Task<AssistantRuleSearch> SearchAsync(string question, CancellationToken cancellationToken);
}

/// <summary>Per chi e su quale gioco cerca un turno, con i numeri di recupero della versione del profilo del turno.</summary>
internal sealed record AssistantRuleScope(Guid GameId, Guid UserId, string? UserRole, AgentProfileContent Profile, string Language);

/// <summary>Crea la sorgente del regolamento di un turno. Il turno la chiede solo se la sessione ha un gioco.</summary>
internal interface IAssistantRuleSourceFactory
{
    IAssistantRuleSource Create(AssistantRuleScope scope);
}
