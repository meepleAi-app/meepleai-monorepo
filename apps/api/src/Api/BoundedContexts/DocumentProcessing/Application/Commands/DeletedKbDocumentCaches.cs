using Api.Services;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #4244: le cache che parlano di un documento della KB e che la sua cancellazione deve
/// svuotare. Finora le svuotavano solo i cambi di stato e di metadati, quindi un documento
/// cancellato restava leggibile fino alla scadenza:
/// <list type="bullet">
/// <item><c>user:{chi l'ha caricato}</c> — l'elenco «I miei documenti»
/// (<c>ListUserKbDocsQueryHandler</c>, cinque minuti);</item>
/// <item><c>kbDoc:{id}</c> — il documento (<c>GetKbDocumentByIdHandler</c>);</item>
/// <item><c>kbChunks:{id}</c> — i suoi passaggi, col testo dentro (<c>GetKbChunksHandler</c>,
/// <c>GetKbChunkByIdHandler</c>);</item>
/// <item><c>recentDocs</c> — il feed «Documenti recenti» di Discover (<c>GetRecentKbDocsQueryHandler</c>,
/// cinque minuti), uguale per ogni utente: #4626.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Non tocca l'etichetta larga <c>user-docs</c>, che <c>PdfStateChangedCacheInvalidationHandler</c>
/// toglie insieme a quella per utente: ogni voce dell'elenco porta entrambe, quindi quella per
/// utente basta, e quella larga svuoterebbe l'elenco di tutti a ogni documento cancellato.
/// </para>
/// <para>
/// <c>recentDocs</c> invece si toglie sempre, anche per un PDF di un gioco privato che nel feed non compare mai
/// (#4626): costa un ricalcolo di una query sola, a una cancellazione rara, e non copia qui il filtro del feed
/// (<c>PrivateGameId == null</c>), che altrimenti potrebbe cambiare senza che questa regola se ne accorga.
/// </para>
/// <para>
/// Non lancia, salvo l'annullamento: una cache che non si svuota vuol dire dati vecchi per qualche
/// minuto, non una cancellazione fallita. Ogni etichetta ha il suo tentativo (#4308): se la prima
/// fallisce — la cache è giù, e con ogni probabilità cadono tutte — le altre vanno provate lo stesso,
/// perché i passaggi di un documento restano leggibili più a lungo dell'elenco.
/// </para>
/// </remarks>
internal static class DeletedKbDocumentCaches
{
    public static async Task InvalidateSafelyAsync(
        IHybridCacheService cache,
        ILogger logger,
        Guid uploadedByUserId,
        Guid pdfId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);

        string[] tags = [$"user:{uploadedByUserId}", $"kbDoc:{pdfId:N}", $"kbChunks:{pdfId:N}", "recentDocs"];

        foreach (var tag in tags)
        {
            try
            {
                await cache.RemoveByTagAsync(tag, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types — best-effort, come le altre cache della cancellazione
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogWarning(ex,
                    "Error invalidating cache tag {Tag} of deleted KB document {PdfId} — stale data may be served until the TTL expires",
                    tag, pdfId);
            }
        }
    }
}
