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
/// Due passi per tag, e il secondo non è un doppione. Il primo è il wrapper
/// <see cref="IHybridCacheService"/> con la retry policy di #613 e il Pub/Sub verso le repliche. La lista
/// e il dettaglio però sono scritti con <see cref="HybridCache"/> grezzo, e il wrapper ha due strade verso
/// una voce così, nessuna delle quali basta da sola:
/// </para>
/// <list type="bullet">
/// <item>Il suo passo locale toglie solo le chiavi elencate nel suo set Redis, che si riempie solo per le
/// voci scritte da lui: per queste non trova nulla (#4561, già incontrato in #4559 e #4585).</item>
/// <item>Il Pub/Sub invece arriva alla voce: <c>HybridCacheInvalidationSubscriber</c> chiama
/// <c>HybridCache.RemoveByTagAsync(tag)</c> su ogni replica che riceve il messaggio, compresa quella che
/// l'ha pubblicato. Ma lo fa in modo asincrono, senza garanzia che arrivi prima della risposta del
/// comando, e senza Redis il messaggio non parte affatto.</item>
/// </list>
/// <para>
/// #4561 ha reso il passo locale del wrapper capace di raggiungere anche le voci grezze (chiama a sua volta
/// <c>HybridCache.RemoveByTagAsync</c>), quindi il passo nativo qui sotto non è più l'unica strada. Resta
/// per una ragione diversa, decisa dal proprietario (#4603, punto 2): viene <b>prima</b> del wrapper.
/// </para>
/// <para>
/// Ogni passo passa dalla retry policy, col nome di operazione che alimenta la metrica
/// (<c>shared-games.list</c> / <c>shared-games.detail</c>). L'ordine per tag è nativo e poi wrapper; la
/// lista precede il dettaglio. Il nativo è locale e immediato; il wrapper dipende da Redis e ha i
/// tentativi: se li esaurisce l'eccezione esce, ma la replica che ha scritto non serve più la voce vecchia,
/// invece di restarne con la cache locale stantia.
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
