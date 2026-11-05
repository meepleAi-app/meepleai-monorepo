namespace Api.Services;

/// <summary>
/// #4659: la politica d'errore comune di chi toglie voci dalla cache DOPO una scrittura già salvata e non
/// passa da <c>SharedGameCacheInvalidator</c> (gli event handler e i job che scrivono i tag a mano).
/// </summary>
/// <remarks>
/// <para>
/// Un passo che fallisce dopo i tentativi della retry policy (#613) si registra (errore, con tag e operazione) e
/// <b>non</b> risale a chi lo ha chiamato. Prima risaliva: l'handler del flag KB, che gira in linea nella
/// pubblicazione di un PDF appena indicizzato, faceva passare a <c>Failed</c> un PDF la cui indicizzazione era
/// riuscita, e la cache restava vecchia lo stesso. La scrittura è già salvata; la scadenza della voce è la rete
/// di sicurezza, e il log è dove si vede. Gli altri passi girano comunque.
/// </para>
/// <para>
/// L'unica eccezione che risale è l'annullamento richiesto dal chiamante (<c>cancellationToken</c>
/// annullato): non è un guasto della cache. Gli event handler passano <see cref="CancellationToken.None"/>
/// (un client che si disconnette fra il commit e l'evizione non deve far saltare l'evizione, come in
/// <c>SharedGameCacheInvalidator</c>); i job passano il token di Quartz.
/// </para>
/// </remarks>
internal static class CacheInvalidationRetryPolicyExtensions
{
    /// <summary>
    /// Esegue un passo di evizione attraverso la retry policy. Restituisce <c>true</c> se è riuscito, <c>false</c>
    /// se si è arreso dopo i tentativi (già registrato nel log).
    /// </summary>
    public static async Task<bool> TryExecuteAsync(
        this ICacheInvalidationRetryPolicy retryPolicy,
        Func<CancellationToken, ValueTask> step,
        string operationName,
        string tag,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(logger);

        try
        {
            await retryPolicy.ExecuteAsync(step, operationName, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // The write is already committed: a failed eviction must not fail the caller (#4659)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(
                ex,
                "Cache eviction failed after the write was committed: tag {Tag}, operation {Operation}. The cached entry stays until it expires",
                tag,
                operationName);
            return false;
        }
    }
}
