namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #4603: il punto in cui i command handler di SharedGameCatalog tolgono dalla cache il read model del
/// catalogo — il dettaglio di un gioco (<c>GET /api/v1/shared-games/{id}</c>), la lista
/// (<c>GET /api/v1/shared-games</c>) e le rail (<c>GET /api/v1/catalog/games/new</c>,
/// <c>GET /api/v1/catalog/trending</c>, #4659).
/// </summary>
/// <remarks>
/// <para>
/// Chi lo chiama oggi: i command handler del BC che cambiano lo stato di un gioco o lo cancellano, o che
/// scrivono cover, traduzioni, errata, FAQ, dati (<c>UpdateSharedGame</c>, import Excel) e accesso RAG, più
/// l'import da BGG quando pubblica subito (<c>AutoPublish</c>, #4659). Non è la garanzia che ogni percorso di scrittura del BC lo chiami. Chi non lo usa e scrive i tag a mano:
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
/// Le rail del catalogo (#4659) sono liste di giochi con un loro tag e mostrano un sottoinsieme ancora più
/// piccolo — titolo, editore, anno, il flag KB nel trending — e solo giochi pubblicati. Cambiano quando
/// cambia un gioco in quei campi o quando un gioco entra o esce dai pubblicati, che sono fra i casi in cui
/// si toglie la lista: i due metodi che tolgono la lista tolgono anche le rail, anche per le scritture che
/// la lista mostra e le rail no (le cover, che le rail risolvono dopo la cache: costano un ricalcolo del
/// trending, non un errore). <see cref="EvictDetailAsync"/> no.
/// </para>
/// <para>
/// Chi scrive i tag a mano (l'elenco sopra) non passa da qui, e nemmeno dalla sua politica d'errore. Due di
/// loro cambiano il flag KB, che il trending porta nella voce, e tolgono le rail da sé:
/// <c>VectorDocumentIndexedForKbFlagHandler</c> e <c>KbFlagDriftAuditJob</c>, con il tag di
/// <see cref="CatalogRailsCache.Tag"/>. <c>ScheduledBulkInvalidationJob</c> invece non le tocca, di proposito:
/// ogni quindici minuti svuoterebbe una voce di 12 ore che costa una scansione di sette giorni di eventi.
/// Se l'evizione delle rail non riesce, l'unica rete è quindi la scadenza della voce.
/// </para>
/// <para>
/// Errori e cancellazione (#4603, punto 2, deciso). L'evizione sta DOPO il commit, quindi non può far
/// fallire una scrittura già salvata e non deve dipendere dal client:
/// </para>
/// <list type="bullet">
/// <item>Un passo che fallisce DOPO i tentativi della retry policy (#613) si registra (errore, con tag,
/// operazione e passo) e NON risale al chiamante: prima la richiesta rispondeva 500 su una scrittura
/// riuscita, e l'admin che riprovava duplicava un'errata o una FAQ. La scadenza della voce resta la rete di
/// sicurezza.</item>
/// <item>Ogni passo gira comunque: se uno fallisce, gli altri partono lo stesso (il wrapper porta anche il
/// messaggio alle altre repliche; i dettagli degli altri giochi non restano indietro).</item>
/// <item>Il token passato dal chiamante si ignora, di proposito: se il client si disconnette fra il commit e
/// l'evizione, il token è annullato e usarlo salterebbe l'evizione lasciando la voce vecchia. I passi
/// girano con <see cref="CancellationToken.None"/>, come `HybridCacheInvalidationSubscriber`. Il parametro
/// resta nell'interfaccia per non toccare i chiamanti.</item>
/// </list>
/// <para>
/// Chi lo chiama non può quindi accorgersi che l'evizione non è riuscita: non c'è un valore di ritorno né
/// un'eccezione. Si vede nel log e nella metrica della retry policy.
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
    /// Toglie dalla cache la lista (tag <c>search-games</c>, tutte le sue voci), le rail del catalogo (tag
    /// <c>catalog</c>, #4659) e il dettaglio di UN gioco (tag <c>shared-game:{id}</c>).
    /// </summary>
    Task EvictDetailAndListAsync(Guid gameId, CancellationToken cancellationToken);

    /// <summary>
    /// Come <see cref="EvictDetailAndListAsync"/> per più giochi di una stessa scrittura (le azioni in
    /// blocco): la lista e le rail si tolgono una volta sola, il dettaglio di ciascun gioco una volta. Con un
    /// elenco vuoto non fa niente, lista e rail comprese.
    /// </summary>
    Task EvictDetailsAndListAsync(IReadOnlyCollection<Guid> gameIds, CancellationToken cancellationToken);
}
