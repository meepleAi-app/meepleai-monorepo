#pragma warning disable MA0048 // File name must match type name - matcher and its result
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>Risolve un nome detto dall'host fra i giocatori della sessione, come lo risolve oggi la partita.</summary>
internal interface IAssistantPlayerMatcher
{
    AssistantPlayerMatch Match(string name, IReadOnlyList<AssistantPlayer> players);
}

internal sealed record AssistantPlayerMatch(Guid? PlayerId, string? Name, bool IsAmbiguous, IReadOnlyList<string> Candidates);
