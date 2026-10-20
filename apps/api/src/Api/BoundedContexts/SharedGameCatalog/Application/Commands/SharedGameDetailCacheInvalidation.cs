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
/// con la retry policy di #613 e il Pub/Sub verso le repliche, come fanno gli altri percorsi di
/// scrittura del BC. Il dettaglio però è scritto con <see cref="HybridCache"/> grezzo, e il wrapper ha
/// due strade verso una voce così, nessuna delle quali basta da sola:
/// </para>
/// <list type="bullet">
/// <item>Il suo passo locale toglie solo le chiavi elencate nel suo set Redis, che si riempie solo per
/// le voci scritte da lui: per il dettaglio non trova nulla (#4561, già incontrato in #4559 e #4585).</item>
/// <item>Il Pub/Sub invece arriva alla voce: <c>HybridCacheInvalidationSubscriber</c> chiama
/// <c>HybridCache.RemoveByTagAsync(tag)</c> su ogni replica che riceve il messaggio, compresa quella
/// che l'ha pubblicato. Ma lo fa in modo asincrono, senza garanzia che arrivi prima della risposta del
/// comando, e senza Redis il messaggio non parte affatto.</item>
/// </list>
/// <para>
/// Il passo nativo serve a questi due casi: la lettura subito dopo la scrittura, sulla replica che ha
/// eseguito il comando (senza aspettare il messaggio), e l'esecuzione senza Redis. Se #4561 renderà il
/// passo locale del wrapper capace di raggiungere le voci grezze, il passo nativo diventerà ridondante.
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
