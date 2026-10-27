using Api.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Implementazione di <see cref="ISharedGameCacheInvalidator"/>. Prende il posto di
/// <c>CoverCacheInvalidation</c> (#4100) e di <c>SharedGameDetailCacheInvalidation</c> (#4565), che
/// facevano la stessa cosa in due varianti, e dell'evizione nativa della lista che
/// <c>SetRagPublicAccessCommandHandler</c> (#4585) aggiungeva a mano.
/// </summary>
/// <remarks>
/// <para>
/// I tag sono scritti qui e solo qui, per i percorsi di scrittura che passano da questo servizio. Sono
/// quelli con cui <c>SearchSharedGamesQueryHandler</c> (lista) e <c>GetSharedGameByIdQueryHandler</c>
/// (dettaglio) scrivono le voci.
/// </para>
/// <para>
/// Due passi per tag: prima il nativo, <c>HybridCache.RemoveByTagAsync</c>, poi il wrapper
/// <see cref="IHybridCacheService"/> con la retry policy di #613 e il Pub/Sub verso le repliche. La lista e il
/// dettaglio sono scritti con <see cref="HybridCache"/> grezzo, e per una voce così:
/// </para>
/// <list type="bullet">
/// <item>fino a #4561 il passo locale del wrapper non trovava nulla: toglieva solo le chiavi del suo set Redis,
/// che si riempie solo per le voci scritte da lui (già incontrato in #4559 e #4585). Ora il wrapper comincia a
/// sua volta con <c>HybridCache.RemoveByTagAsync</c>;</item>
/// <item>il Pub/Sub arriva alla voce sulle altre repliche (<c>HybridCacheInvalidationSubscriber</c> chiama
/// <c>HybridCache.RemoveByTagAsync(tag)</c> su ognuna), ma in modo asincrono e, senza Redis, non parte affatto.</item>
/// </list>
/// <para>
/// Il passo nativo qui resta, e viene <b>prima</b> del wrapper, per decisione del proprietario (#4603, punto 2).
/// È una cautela più che un guadagno misurato: dopo #4561 il wrapper fa la stessa evizione come prima cosa,
/// quindi l'ordine conta solo se qualcosa fallisce prima di quella riga. Ha però un effetto che oggi lo rende
/// necessario nei test: l'host di integrazione sostituisce il wrapper con un passthrough, e senza il passo
/// nativo qui nessuna evizione avverrebbe (<c>SharedGameCatalogEndpointsIntegrationTests</c>, <c>StatusCache</c>).
/// </para>
/// <para>
/// Ogni passo passa dalla retry policy, col nome di operazione che alimenta la metrica
/// (<c>shared-games.list</c> / <c>shared-games.detail</c>). La lista precede il dettaglio. Se il wrapper esaurisce
/// i tentativi l'eccezione esce, a evizione nativa già fatta; se si arrende il passo nativo, il wrapper non parte.
/// </para>
/// </remarks>
internal sealed class SharedGameCacheInvalidator : ISharedGameCacheInvalidator
{
    private const string SearchGamesTag = "search-games";
    private const string ListOperation = "shared-games.list";
    private const string DetailOperation = "shared-games.detail";

    private readonly IHybridCacheService _cache;
    private readonly ICacheInvalidationRetryPolicy _retryPolicy;
    private readonly HybridCache _hybridCache;

    public SharedGameCacheInvalidator(
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        HybridCache hybridCache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
    }

    /// <inheritdoc />
    public Task EvictDetailAsync(Guid gameId, CancellationToken cancellationToken) =>
        EvictTagAsync(DetailTag(gameId), DetailOperation, cancellationToken);

    /// <inheritdoc />
    public Task EvictDetailAndListAsync(Guid gameId, CancellationToken cancellationToken) =>
        EvictDetailsAndListAsync([gameId], cancellationToken);

    /// <inheritdoc />
    public async Task EvictDetailsAndListAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gameIds);

        if (gameIds.Count == 0)
        {
            return;
        }

        await EvictTagAsync(SearchGamesTag, ListOperation, cancellationToken).ConfigureAwait(false);

        foreach (var gameId in gameIds)
        {
            await EvictTagAsync(DetailTag(gameId), DetailOperation, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string DetailTag(Guid gameId) => $"shared-game:{gameId}";

    private async Task EvictTagAsync(string tag, string operationName, CancellationToken cancellationToken)
    {
        // Nativo prima (#4603, punto 2): vedi la nota di classe.
        await _retryPolicy.ExecuteAsync(
            token => _hybridCache.RemoveByTagAsync(tag, token),
            operationName,
            cancellationToken).ConfigureAwait(false);

        await _retryPolicy.ExecuteAsync(
            token => new ValueTask(_cache.RemoveByTagAcrossReplicasAsync(tag, token)),
            operationName,
            cancellationToken).ConfigureAwait(false);
    }
}
