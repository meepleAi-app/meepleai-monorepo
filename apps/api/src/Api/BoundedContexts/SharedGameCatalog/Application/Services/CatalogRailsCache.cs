namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #4659: il tag con cui le rail del catalogo (<c>GetNewGamesQueryHandler</c>, <c>GetCatalogTrendingQueryHandler</c>)
/// scrivono le loro voci in cache, e con cui <see cref="SharedGameCacheInvalidator"/> le toglie. Sta in un solo
/// posto perché chi scrive e chi evince devono dire la stessa stringa: se divergessero, l'evizione non
/// troverebbe niente e nessun errore lo direbbe.
/// </summary>
internal static class CatalogRailsCache
{
    /// <summary>Il tag comune alle voci di tutte le rail del catalogo.</summary>
    public const string Tag = "catalog";
}
