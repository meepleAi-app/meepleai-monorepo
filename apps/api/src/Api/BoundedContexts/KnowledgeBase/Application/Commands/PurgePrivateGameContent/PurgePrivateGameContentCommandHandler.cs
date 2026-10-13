using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
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
/// <b>Le tre cancellazioni per gioco</b> che seguono sono la rete sotto: valgono per le righe che
/// non pendono da un PDF del gioco. Gli embedding in particolare non hanno una chiave esterna
/// verso il documento vettoriale, e <c>game_id</c> è il loro unico legame col gioco.
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
    private readonly ILogger<PurgePrivateGameContentCommandHandler> _logger;

    public PurgePrivateGameContentCommandHandler(
        MeepleAiDbContext db,
        IMediator mediator,
        ILogger<PurgePrivateGameContentCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(PurgePrivateGameContentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var gameId = command.PrivateGameId;

        var pdfIds = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.PrivateGameId == gameId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var pdfId in pdfIds)
        {
            try
            {
                await _mediator.Send(new DeleteKbDocumentCommand(pdfId), cancellationToken).ConfigureAwait(false);
            }
            catch (NotFoundException)
            {
                // Qualcun altro l'ha cancellato fra la lettura e qui: è lo stato che volevamo.
            }
        }

        var embeddings = await _db.PgVectorEmbeddings
            .Where(e => e.GameId == gameId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var passages = await _db.TextChunks
            .Where(c => c.PrivateGameId == gameId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        var vectorDocuments = await _db.VectorDocuments
            .Where(v => v.PrivateGameId == gameId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        // I messaggi stanno nella riga del thread; citazioni, raccolte e link di condivisione
        // seguono per cascata.
        var threads = await _db.ChatThreads
            .Where(t => t.PrivateGameId == gameId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Purged private game {PrivateGameId}: {Pdfs} PDFs, {Threads} threads, and outside those PDFs "
            + "{Embeddings} embeddings, {Passages} passages, {VectorDocuments} vector documents",
            gameId, pdfIds.Count, threads, embeddings, passages, vectorDocuments);
    }
}
