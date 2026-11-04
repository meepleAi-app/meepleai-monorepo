using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminKbFeedback;

/// <summary>
/// #4469: ciò che serve a leggere un feedback senza id: il nome del gioco e il numero della versione.
/// Un feedback può stare su un gioco del catalogo o su un gioco privato, perché il thread lo permette.
/// </summary>
internal static class AdminKbFeedbackLabels
{
    public static async Task<IReadOnlyDictionary<Guid, AgentProfileVersion>> VersionsAsync(
        IAgentProfileRepository profiles, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetAsync(cancellationToken).ConfigureAwait(false);
        return profile is null
            ? new Dictionary<Guid, AgentProfileVersion>()
            : profile.Versions.ToDictionary(v => v.Id);
    }

    public static async Task<IReadOnlyDictionary<Guid, string>> GameNamesAsync(
        MeepleAiDbContext db, IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken)
    {
        if (gameIds.Count == 0)
            return new Dictionary<Guid, string>();

        var names = await db.SharedGames
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Title })
            .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken)
            .ConfigureAwait(false);

        var missing = gameIds.Where(id => !names.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            var privateNames = await db.PrivateGames
                .AsNoTracking()
                .Where(g => missing.Contains(g.Id))
                .Select(g => new { g.Id, g.Title })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var game in privateNames)
                names[game.Id] = game.Title;
        }

        return names;
    }
}
