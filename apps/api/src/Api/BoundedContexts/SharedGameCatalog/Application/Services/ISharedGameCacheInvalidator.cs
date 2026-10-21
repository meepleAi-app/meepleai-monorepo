namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #4603: il punto in cui i command handler di SharedGameCatalog tolgono dalla cache il read model del
/// catalogo — il dettaglio di un gioco (<c>GET /api/v1/shared-games/{id}</c>) e la lista
/// (<c>GET /api/v1/shared-games</c>).
/// </summary>
/// <remarks>
/// <para>
/// Chi lo chiama oggi: i command handler del BC che cambiano lo stato di un gioco o lo cancellano, o che
/// scrivono cover, traduzioni, errata, FAQ, dati (<c>UpdateSharedGame</c>, import Excel) e accesso RAG. Non
/// è la garanzia che ogni percorso di scrittura del BC lo chiami. Chi non lo usa e scrive i tag a mano:
/// gli event handler e i job <c>PdfCoverGeneratedEventHandler</c>,
/// <c>VectorDocumentIndexedForKbFlagHandler</c>, <c>ToolkitChangedForCatalogAggregatesHandler</c>,
/// <c>AgentDefinitionChangedForCatalogAggregatesHandler</c>, <c>ScheduledBulkInvalidationJob</c> e
/// <c>KbFlagDriftAuditJob</c> (quest'ultimo vive in DocumentProcessing). Migrarli è un seguito di #4603.
/// </para>
/// <para>
/// Da chiamare DOPO il salvataggio: un'evizione prima lascerebbe che una lettura ripopoli la cache col
/// contenuto vecchio, prima che la scrittura sia visibile.
/// </para>
/// <para>
/// Quando tocca <see cref="EvictDetailAsync"/> e quando <see cref="EvictDetailAndListAsync"/>. Il
/// dettaglio porta tutto del gioco, compreso <c>status</c>: qualunque scrittura che ne cambia un campo lo
/// invalida. La lista (<c>SharedGameDto</c>) mostra un sottoinsieme — titolo, cover, <c>isRagPublic</c>,
/// i flag di KB e toolkit — e include solo i giochi pubblicati: la toccano le scritture su quei campi e
/// ogni cambio di stato o cancellazione, perché fa entrare o uscire il gioco dalla lista. Errata e FAQ,
/// che la lista non riporta, la lasciano com'è.
/// </para>
/// <para>
/// Errori e cancellazione. Un'evizione che fallisce DOPO i tentativi della retry policy (#613) risale al
/// chiamante, e il token è quello della richiesta. Cosa fare di una scrittura già salvata quando
/// l'evizione fallisce, o quando il client si è disconnesso, è la questione aperta del punto 2 di #4603:
/// quando sarà deciso, si cambia qui e in nessun altro posto.
/// </para>
/// </remarks>
internal interface ISharedGameCacheInvalidator
{
    /// <summary>
    /// Toglie dalla cache il dettaglio di UN gioco (tag <c>shared-game:{id}</c>) e nient'altro: non la
    /// lista, non il dettaglio degli altri giochi.
    /// </summary>
    Task EvictDetailAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>
    /// Toglie dalla cache la lista (tag <c>search-games</c>, tutte le sue voci) e il dettaglio di UN
    /// gioco (tag <c>shared-game:{id}</c>).
    /// </summary>
    Task EvictDetailAndListAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>
    /// Come <see cref="EvictDetailAndListAsync"/> per più giochi di una stessa scrittura (le azioni in
    /// blocco): la lista si toglie una volta sola, il dettaglio di ciascun gioco una volta. Con un
    /// elenco vuoto non fa niente, lista compresa.
    /// </summary>
    Task EvictDetailsAndListAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken);
}
