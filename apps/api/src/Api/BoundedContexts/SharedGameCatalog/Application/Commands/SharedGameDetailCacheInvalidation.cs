using Api.Services;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// #4565: toglie dalla cache il dettaglio di UN gioco (<c>GET /api/v1/shared-games/{id}</c>) dopo una
/// scrittura che ne cambia il contenuto senza passare da <see cref="CoverCacheInvalidation"/>: le
/// errata e le FAQ.
/// </summary>
/// <remarks>
/// <para>
/// Tocca solo il tag di dettaglio <c>shared-game:{id}</c>, non <c>search-games</c>: la lista del
/// catalogo (<c>SharedGameDto</c>) non porta né errata né FAQ, quindi una scrittura qui non la cambia.
/// Il tag è quello con cui <c>GetSharedGameByIdQueryHandler</c> scrive la voce.
/// </para>
/// <para>
/// Due passi, e il secondo non è un doppione. Il primo è il wrapper <see cref="IHybridCacheService"/>
/// con la retry policy di #613 e il Pub/Sub verso le altre repliche, come fanno gli altri percorsi di
/// scrittura del BC. Ma il wrapper toglie solo le chiavi elencate nel suo set Redis, che si riempie
/// solo per le voci scritte da lui, e il dettaglio è scritto con <see cref="HybridCache"/> grezzo: è
/// il limite di #4561, già incontrato in #4559 e in #4585. Il tag nativo raggiunge la voce su questa
/// istanza, anche senza Redis; il Pub/Sub del primo passo copre le altre repliche. Quando #4561 renderà
/// il wrapper capace di raggiungere le voci grezze, il secondo passo si può togliere.
/// </para>
/// <para>
/// Da chiamare DOPO il salvataggio: un'evizione prima lascerebbe che una lettura ripopoli la cache col
/// contenuto vecchio, prima che la scrittura sia visibile.
/// </para>
/// </remarks>
internal static class SharedGameDetailCacheInvalidation
{
    public static async Task EvictAsync(
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        HybridCache hybridCache,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentNullException.ThrowIfNull(hybridCache);

        var tag = $"shared-game:{gameId}";

        await retryPolicy.ExecuteAsync(
            token => new ValueTask(cache.RemoveByTagAcrossReplicasAsync(tag, token)),
            "shared-games.detail",
            cancellationToken).ConfigureAwait(false);

        await retryPolicy.ExecuteAsync(
            token => hybridCache.RemoveByTagAsync(tag, token),
            "shared-games.detail",
            cancellationToken).ConfigureAwait(false);
    }
}
