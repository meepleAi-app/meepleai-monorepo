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
/// <c>GetKbChunkByIdHandler</c>).</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// Non tocca l'etichetta larga <c>user-docs</c>, che <c>PdfStateChangedCacheInvalidationHandler</c>
/// toglie insieme a quella per utente: ogni voce dell'elenco porta entrambe, quindi quella per
/// utente basta, e quella larga svuoterebbe l'elenco di tutti a ogni documento cancellato.
/// </para>
/// <para>
/// Non lancia: una cache che non si svuota vuol dire dati vecchi per qualche minuto, non una
/// cancellazione fallita.
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

        try
        {
            await cache.RemoveByTagAsync($"user:{uploadedByUserId}", cancellationToken).ConfigureAwait(false);
            await cache.RemoveByTagAsync($"kbDoc:{pdfId:N}", cancellationToken).ConfigureAwait(false);
            await cache.RemoveByTagAsync($"kbChunks:{pdfId:N}", cancellationToken).ConfigureAwait(false);
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
                "Error invalidating the caches of deleted KB document {PdfId} — stale data may be served until the TTL expires",
                pdfId);
        }
    }
}
