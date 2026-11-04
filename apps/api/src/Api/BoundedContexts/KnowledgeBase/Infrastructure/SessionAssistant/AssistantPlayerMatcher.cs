using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// Adapter verso la risoluzione dei nomi della partita (esatta, nome di battesimo, contenimento): gli
/// strumenti non dipendono da GameManagement (spec §3.2), ma risolvono i nomi allo stesso modo.
/// </summary>
internal sealed class AssistantPlayerMatcher(IPlayerNameResolutionService resolution) : IAssistantPlayerMatcher
{
    public AssistantPlayerMatch Match(string name, IReadOnlyList<AssistantPlayer> players)
    {
        var result = resolution.ResolvePlayer(name, players.ToDictionary(p => p.Id, p => p.Name));
        if (result.IsAmbiguous)
        {
            return new AssistantPlayerMatch(null, null, true, result.Candidates.Select(c => c.Name).ToList());
        }

        return result.IsResolved
            ? new AssistantPlayerMatch(result.PlayerId, result.ResolvedName, false, [])
            : new AssistantPlayerMatch(null, null, false, []);
    }
}
