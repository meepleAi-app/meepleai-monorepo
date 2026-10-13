using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.PurgePrivateGameContent;

/// <summary>
/// #4244 (consegna C). La cancellazione di un gioco privato è logica (<c>is_deleted</c>), quindi
/// le chiavi esterne con <c>ON DELETE CASCADE</c> verso <c>private_games</c> non scattano mai. Il
/// contenuto va tolto qui, in modo esplicito.
/// </summary>
/// <remarks>
/// <para>
/// <b>I PDF</b> passano dalla cancellazione canonica, <see cref="DeleteKbDocumentCommand"/>: toglie
/// gli embedding, la riga (e per cascata passaggi e documento vettoriale), il file, le cache. È la
/// stessa strada che <c>OrphanPdfCleanupSeeder</c> usa per i PDF di un gioco del catalogo che non
/// esiste più. Tenere il PDF di un gioco cancellato lascerebbe un file che l'utente non può più
/// raggiungere dal suo gioco, elencato come pronto senza più un indice.
/// </para>
/// <para>
/// <b>Un PDF che un altro gioco usa ancora resta.</b> La deduplicazione non ricarica due volte lo
/// stesso file: al secondo gioco dà un collegamento verso il PDF del primo
/// (<c>CompleteChunkedUploadCommandHandler</c>). È la regola di #2949: un PDF collegato da più
/// giochi si scollega, non si distrugge. Quel PDF resta legato al gioco cancellato finché
/// qualcuno lo usa, e se ne va con l'ultimo gioco che lo usa: per questo qui si guardano anche i
/// PDF a cui il gioco era collegato e che sono rimasti senza il gioco che li aveva caricati.
/// </para>
/// <para>
/// <b>Le tre cancellazioni per gioco</b> che seguono sono la rete sotto: valgono per le righe che
/// non pendono da un PDF cancellato qui. Gli embedding in particolare non hanno una chiave
/// esterna verso il documento vettoriale, e <c>game_id</c> è il loro unico legame col gioco.
/// </para>
/// <para>
/// <b>Si può ripetere.</b> Ogni passo cancella ciò che trova: se la richiesta si ferma a metà, o
/// due richieste si incrociano, rifarla porta allo stesso stato.
/// </para>
/// </remarks>
internal sealed class PurgePrivateGameContentCommandHandler : ICommandHandler<PurgePrivateGameContentCommand>
{
    private readonly MeepleAiDbContext _db;
    private readonly IMediator _mediator;
    private readonly IHybridCacheService _hybridCache;
    private readonly ILogger<PurgePrivateGameContentCommandHandler> _logger;

    public PurgePrivateGameContentCommandHandler(
        MeepleAiDbContext db,
        IMediator mediator,
        IHybridCacheService hybridCache,
        ILogger<PurgePrivateGameContentCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PurgePrivateGameContentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var gameId = command.PrivateGameId;

        // 1. I collegamenti del gioco. Prima si annotano i PDF a cui portavano, poi si tolgono:
        //    un gioco cancellato non usa più niente, e non deve tenere in vita i PDF degli altri.
        var linkedPdfIds = await _db.EntityLinks
            .IgnoreQueryFilters()
            .Where(l => !l.IsDeleted
                        && l.SourceEntityType == MeepleEntityType.Game && l.SourceEntityId == gameId
                        && l.TargetEntityType == MeepleEntityType.KbCard)
            .Select(l => l.TargetEntityId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var links = await _db.EntityLinks
            .IgnoreQueryFilters()
            .Where(l => !l.IsDeleted
                        && l.SourceEntityType == MeepleEntityType.Game && l.SourceEntityId == gameId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(l => l.IsDeleted, true)
                      .SetProperty(l => l.DeletedAt, now)
                      .SetProperty(l => l.UpdatedAt, now),
                cancellationToken).ConfigureAwait(false);

        // 2. I PDF in gioco: quelli caricati per questo gioco, e quelli a cui era collegato e che
        //    aspettavano solo lui, perché il gioco che li aveva caricati è già stato cancellato.
        var candidates = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.PrivateGameId == gameId
                        || (linkedPdfIds.Contains(p.Id)
                            && _db.PrivateGames.IgnoreQueryFilters()
                                .Any(g => g.Id == p.PrivateGameId && g.IsDeleted)))
            .Select(p => new { p.Id, p.UploadedByUserId, p.PrivateGameId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var candidateIds = candidates.Select(p => p.Id).ToList();

        // 3. Quelli che qualcun altro usa ancora: un collegamento vivo, da un gioco che non sia
        //    un gioco privato già cancellato. I collegamenti di questo gioco sono appena andati.
        var stillUsed = await _db.EntityLinks
            .IgnoreQueryFilters()
            .Where(l => !l.IsDeleted
                        && l.TargetEntityType == MeepleEntityType.KbCard
                        && candidateIds.Contains(l.TargetEntityId)
                        && !_db.PrivateGames.IgnoreQueryFilters()
                            .Any(g => g.Id == l.SourceEntityId && g.IsDeleted))
            .Select(l => l.TargetEntityId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var deletedPdfs = 0;
        foreach (var pdf in candidates.Where(p => !stillUsed.Contains(p.Id)))
        {
            try
            {
                await _mediator.Send(new DeleteKbDocumentCommand(pdf.Id), cancellationToken).ConfigureAwait(false);
                deletedPdfs++;
            }
            catch (NotFoundException)
            {
                // Qualcun altro l'ha cancellato fra la lettura e qui: è lo stato che volevamo.
            }

            // Il comando svuota queste cache da dentro la sua transazione. Qui il commit c'è
            // stato: una lettura caduta nel mezzo non lascia il documento in cache.
            await DeletedKbDocumentCaches
                .InvalidateSafelyAsync(_hybridCache, _logger, pdf.UploadedByUserId, pdf.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        // 4. La rete: ciò che porta l'id del gioco senza pendere da un PDF cancellato qui sopra.
        //    Restano fuori le righe dei PDF tenuti perché un altro gioco li usa.
        var keptPdfIds = candidates
            .Where(p => p.PrivateGameId == gameId && stillUsed.Contains(p.Id))
            .Select(p => p.Id)
            .ToList();
        var keptVectorDocumentIds = await _db.VectorDocuments
            .Where(v => keptPdfIds.Contains(v.PdfDocumentId))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var embeddings = await _db.PgVectorEmbeddings
            .Where(e => e.GameId == gameId && !keptVectorDocumentIds.Contains(e.VectorDocumentId))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var passages = await _db.TextChunks
            .Where(c => c.PrivateGameId == gameId && !keptPdfIds.Contains(c.PdfDocumentId))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var vectorDocuments = await _db.VectorDocuments
            .Where(v => v.PrivateGameId == gameId && !keptPdfIds.Contains(v.PdfDocumentId))
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // 5. I caricamenti a pezzi lasciati a metà: completati dopo, creerebbero un PDF su un
        //    gioco che non c'è più.
        var uploads = await _db.ChunkedUploadSessions
            .Where(u => u.PrivateGameId == gameId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // 6. Le chat. I messaggi stanno nella riga del thread; citazioni, raccolte e link di
        //    condivisione seguono per cascata.
        var threads = await _db.ChatThreads
            .Where(t => t.PrivateGameId == gameId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Purged private game {PrivateGameId}: {Pdfs} PDFs deleted, {KeptPdfs} kept because another game still uses them, "
            + "{Threads} threads, {Links} links, {Uploads} open uploads, and outside the deleted PDFs "
            + "{Embeddings} embeddings, {Passages} passages, {VectorDocuments} vector documents",
            gameId, deletedPdfs, keptPdfIds.Count, threads, links, uploads, embeddings, passages, vectorDocuments);
    }
}
