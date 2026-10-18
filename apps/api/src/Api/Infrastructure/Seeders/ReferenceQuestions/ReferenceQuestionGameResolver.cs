namespace Api.Infrastructure.Seeders.ReferenceQuestions;

/// <summary>Un gioco del catalogo (<c>shared_games</c>, non cancellato) candidato per un titolo del set.</summary>
internal sealed record CatalogGame(Guid Id, string Title);

/// <summary>Come si risolve il titolo di un gioco del set nel catalogo di questo ambiente.</summary>
internal abstract record GameResolution
{
    private GameResolution() { }

    /// <summary>Un solo gioco con quel titolo.</summary>
    public sealed record Found(Guid SharedGameId) : GameResolution;

    /// <summary>Nessun gioco con quel titolo.</summary>
    public sealed record Missing : GameResolution;

    /// <summary>Più giochi con quel titolo: il seeder non sceglie.</summary>
    public sealed record Ambiguous(IReadOnlyList<Guid> Candidates) : GameResolution;
}

/// <summary>
/// #4470: il set non porta UUID, perché l'id di un gioco cambia da un ambiente all'altro. Il gioco si
/// risolve per titolo <b>esatto</b> (stesse maiuscole, nessuno spazio tolto): «Catan» non prende
/// «The Settlers of Catan», doppione del catalogo senza documenti.
/// </summary>
internal static class ReferenceQuestionGameResolver
{
    public static IReadOnlyDictionary<string, GameResolution> Resolve(
        IEnumerable<string> titles, IEnumerable<CatalogGame> catalog)
    {
        ArgumentNullException.ThrowIfNull(titles);
        ArgumentNullException.ThrowIfNull(catalog);

        var byTitle = catalog
            .GroupBy(g => g.Title, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList(), StringComparer.Ordinal);

        var resolved = new Dictionary<string, GameResolution>(StringComparer.Ordinal);
        foreach (var title in titles)
        {
            if (resolved.ContainsKey(title))
                continue;

            if (!byTitle.TryGetValue(title, out var ids))
                resolved[title] = new GameResolution.Missing();
            else if (ids.Count == 1)
                resolved[title] = new GameResolution.Found(ids[0]);
            else
                resolved[title] = new GameResolution.Ambiguous(ids);
        }

        return resolved;
    }
}
