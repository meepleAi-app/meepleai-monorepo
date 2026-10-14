using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.PurgePrivateGameContent;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.Cleanup;

/// <summary>
/// #4244, consegna C2 — il pregresso. Ripulisce i giochi privati cancellati che hanno ancora
/// del contenuto: chat, PDF, passaggi, documenti vettoriali, embedding, caricamenti aperti,
/// collegamenti.
/// </summary>
/// <remarks>
/// <para>
/// La pulizia (<see cref="PurgePrivateGameContentCommand"/>) parte quando un gioco privato viene
/// cancellato. Resta fuori ciò che quella strada non vede:
/// </para>
/// <list type="bullet">
/// <item>i giochi cancellati <b>prima</b> che la pulizia esistesse: la riga è segnata cancellata,
/// il contenuto è ancora tutto lì;</item>
/// <item>un PDF tenuto perché un altro gioco lo collegava, quando poi quel collegamento se ne va
/// senza che l'altro gioco venga cancellato;</item>
/// <item>un PDF arrivato su un gioco mentre veniva cancellato, o scritto da un'indicizzazione
/// ancora in corso.</item>
/// </list>
/// <para>
/// Non reimplementa niente: invia lo stesso comando, gioco per gioco. Ciò che la pulizia tiene
/// (un PDF che chi l'ha caricato usa ancora da un altro gioco) lo tiene anche qui; quel gioco
/// verrà rivisitato a ogni avvio finché il PDF è in uso, senza effetto.
/// </para>
/// <para>
/// È uno strato di seed perché è lì che stanno i lavori da fare una volta all'avvio, sotto il
/// lock dell'orchestratore (vedi <c>OrphanPdfCleanupSeeder</c> per i PDF del catalogo). Sta in
/// uno strato suo, e non in quello del catalogo, perché non deve saltare con
/// <c>SKIP_CATALOG_SEED</c>. <b>Non lancia mai</b>: l'orchestratore non protegge gli strati, e
/// un'eccezione qui salterebbe tutti quelli dopo.
/// </para>
/// </remarks>
internal sealed class DeletedPrivateGameContentCleanupLayer : ISeedLayer
{
    public string Name => "DeletedPrivateGameContentCleanup";

    /// <summary>Gira in ogni profilo che semina: non dipende dai dati del catalogo.</summary>
    public SeedProfile MinimumProfile => SeedProfile.Prod;

    public async Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var gameIds = await FindDeletedGamesWithContentAsync(context.DbContext, cancellationToken)
                .ConfigureAwait(false);
            if (gameIds.Count == 0)
            {
                context.Logger.LogInformation("[{Layer}] no deleted private game has content left", Name);
                return;
            }

            var mediator = context.Services.GetRequiredService<IMediator>();
            var cleaned = 0;
            var failed = 0;
            foreach (var gameId in gameIds)
            {
                try
                {
                    await mediator.Send(new PurgePrivateGameContentCommand(gameId), cancellationToken)
                        .ConfigureAwait(false);
                    cleaned++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Un gioco che non si riesce a pulire non ferma gli altri: ci si riprova
                    // al prossimo avvio. Il contesto condiviso può avere entità a metà.
                    context.Logger.LogWarning(
                        ex, "[{Layer}] could not purge deleted private game {PrivateGameId}. Continuing.", Name, gameId);
                    context.DbContext.ChangeTracker.Clear();
                    failed++;
                }
            }

            context.Logger.LogInformation(
                "[{Layer}] {Cleaned} deleted private games purged, {Failed} failed and left for the next run",
                Name, cleaned, failed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            context.Logger.LogError(ex, "[{Layer}] failed — continuing", Name);
        }
    }

    /// <summary>
    /// I giochi privati cancellati di cui resta qualcosa. <c>IgnoreQueryFilters</c> vale per tutta
    /// la query: serve per vedere i giochi cancellati, e fa contare anche le righe che un filtro
    /// nasconderebbe — è ciò che si vuole, sono righe che devono sparire.
    /// </summary>
    internal static Task<List<Guid>> FindDeletedGamesWithContentAsync(MeepleAiDbContext db, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);

        return db.PrivateGames
            .IgnoreQueryFilters()
            .Where(g => g.IsDeleted
                        && (db.PdfDocuments.Any(p => p.PrivateGameId == g.Id)
                            || db.ChatThreads.Any(t => t.PrivateGameId == g.Id)
                            || db.TextChunks.Any(c => c.PrivateGameId == g.Id)
                            || db.VectorDocuments.Any(v => v.PrivateGameId == g.Id)
                            || db.PgVectorEmbeddings.Any(e => e.GameId == g.Id)
                            || db.ChunkedUploadSessions.Any(u => u.PrivateGameId == g.Id)
                            || db.EntityLinks.Any(l => !l.IsDeleted
                                                       && l.SourceEntityType == MeepleEntityType.Game
                                                       && l.SourceEntityId == g.Id)))
            .OrderBy(g => g.DeletedAt)
            .Select(g => g.Id)
            .ToListAsync(ct);
    }
}
