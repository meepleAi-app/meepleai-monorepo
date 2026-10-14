using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.PurgePrivateGameContent;
using Api.Infrastructure.Entities.UserLibrary;
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
/// verrà rivisitato a ogni avvio finché il PDF è in uso, senza effetto e senza togliere posto agli
/// altri nel tetto per avvio (<see cref="MaxGamesPerRun"/>).
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

    /// <summary>
    /// Quanti giochi al massimo per avvio. L'avvio aspetta i seed, e ogni PDF costa una
    /// cancellazione con il suo file: il pregresso, di cui non si conosce il volume, si smaltisce a
    /// rate, dal gioco cancellato da più tempo.
    /// </summary>
    internal int MaxGamesPerRun { get; init; } = 50;

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
            var purged = 0;
            var kept = 0;
            var failed = 0;
            foreach (var gameId in gameIds)
            {
                // Il tetto conta solo il lavoro vero: un gioco che si è svuotato, o che è fallito.
                // Un gioco che tiene soltanto un PDF ancora in uso da un altro gioco resta in lista a
                // ogni avvio, e fra i più vecchi: se contasse, a tetto pieno di giochi così nessun
                // altro verrebbe mai pulito. Visitarlo costa qualche lettura, non una cancellazione.
                if (purged + failed >= MaxGamesPerRun)
                {
                    break;
                }

                try
                {
                    await mediator.Send(new PurgePrivateGameContentCommand(gameId), cancellationToken)
                        .ConfigureAwait(false);
                    if (await StillHasContentAsync(context.DbContext, gameId, cancellationToken).ConfigureAwait(false))
                    {
                        kept++;
                    }
                    else
                    {
                        purged++;
                    }
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
                "[{Layer}] {Purged} deleted private games purged, {Kept} keep a PDF another game still uses, "
                + "{Failed} failed, {Left} left for the next run (limit {Limit} per run)",
                Name, purged, kept, failed, gameIds.Count - purged - kept - failed, MaxGamesPerRun);
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

        return DeletedGamesWithContent(db)
            .OrderBy(g => g.DeletedAt)
            .Select(g => g.Id)
            .ToListAsync(ct);
    }

    private static Task<bool> StillHasContentAsync(MeepleAiDbContext db, Guid gameId, CancellationToken ct) =>
        DeletedGamesWithContent(db).AnyAsync(g => g.Id == gameId, ct);

    private static IQueryable<PrivateGameEntity> DeletedGamesWithContent(MeepleAiDbContext db)
    {
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
                                                       && l.SourceEntityId == g.Id)));
    }
}
