using Api.Services;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;

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
/// (<c>shared-games.list</c> / <c>shared-games.detail</c>). La lista precede il dettaglio.
/// </para>
/// <para>
/// Errori (#4603, punto 2): l'evizione sta dopo il commit, quindi un passo che esaurisce i tentativi si
/// registra e non si rilancia, e gli altri passi girano comunque; i passi usano <see cref="CancellationToken.None"/>
/// e ignorano il token della richiesta. Vedi <see cref="ISharedGameCacheInvalidator"/>.
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
    private readonly ILogger<SharedGameCacheInvalidator> _logger;

    public SharedGameCacheInvalidator(
        IHybridCacheService cache,
        ICacheInvalidationRetryPolicy retryPolicy,
        HybridCache hybridCache,
        ILogger<SharedGameCacheInvalidator>? logger = null)
    {
        _logger = logger ?? NullLogger<SharedGameCacheInvalidator>.Instance;
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
    }

    /// <inheritdoc />
    public Task EvictDetailAsync(Guid gameId, CancellationToken cancellationToken) =>
        EvictTagAsync(DetailTag(gameId), DetailOperation);

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

        await EvictTagAsync(SearchGamesTag, ListOperation).ConfigureAwait(false);

        foreach (var gameId in gameIds)
        {
            await EvictTagAsync(DetailTag(gameId), DetailOperation).ConfigureAwait(false);
        }
    }

    private static string DetailTag(Guid gameId) => $"shared-game:{gameId}";

    private async Task EvictTagAsync(string tag, string operationName)
    {
        // Nativo prima (#4603, punto 2): vedi la nota di classe. Ogni passo gira anche se l'altro è fallito.
        await TryEvictAsync(token => _hybridCache.RemoveByTagAsync(tag, token), tag, operationName, "native")
            .ConfigureAwait(false);

        await TryEvictAsync(token => new ValueTask(_cache.RemoveByTagAcrossReplicasAsync(tag, token)), tag, operationName, "wrapper")
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Un passo di evizione dopo il commit: CancellationToken.None (il client potrebbe essersi disconnesso) e
    /// nessun rilancio. La scrittura è già salvata; un'evizione non riuscita lascia la voce vecchia fino alla sua
    /// scadenza, ed è un fatto da vedere nel log, non un errore da restituire a chi ha scritto.
    /// </summary>
    private async Task TryEvictAsync(Func<CancellationToken, ValueTask> step, string tag, string operationName, string stepName)
    {
        try
        {
            await _retryPolicy.ExecuteAsync(step, operationName, CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // The write is already committed: a failed eviction must not fail the caller (#4603)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(
                ex,
                "Cache eviction failed after the write was committed: tag {Tag}, operation {Operation}, step {Step}. The cached entry stays until it expires",
                tag,
                operationName,
                stepName);
        }
    }
}
