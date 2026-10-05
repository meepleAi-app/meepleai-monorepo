using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.Testing.Infrastructure;

/// <summary>
/// Issue #4054 — l'unico punto di scrittura dei seeder E2E, perché la violazione di un vincolo
/// durante il loro <c>SaveChanges</c> è un <b>conflitto prevedibile con lo stato del database</b>
/// e non un guasto del server: va detta con un 4xx che nomina il vincolo.
/// </summary>
/// <remarks>
/// <para>
/// 🔴 Perché serve una rete e non bastano le guardie nei singoli handler. Una guardia pre-INSERT
/// copre il vincolo che qualcuno si è ricordato di controllare, e lo copre fino all'istante del
/// controllo: sotto due worker Playwright (<c>fullyParallel</c> con <c>workers: 2</c> in locale)
/// la finestra tra l'<c>AnyAsync</c> e il <c>SaveChanges</c> è reale. Senza questa traduzione
/// quella collisione torna a essere un <c>internal_server_error</c>: la <c>DbUpdateException</c>
/// non è riconosciuta da <c>ApiExceptionHandlerMiddleware</c> — che tratta a parte solo la
/// sottoclasse <c>DbUpdateConcurrencyException</c> — e finisce nel fallback, il cui corpo dice
/// «An unexpected error occurred» più, in Development, <c>ex.StackTrace</c>. Il nome del vincolo
/// vive in <c>InnerException.Message</c>: non arrivava al chiamante per nessuna via.
/// </para>
/// <para>
/// La misura che giustifica il raggio, al 2026-10-04. Dai vincoli delle tabelle su cui i cinque
/// seeder scrivono, tre collisioni sono raggiungibili e una no:
/// </para>
/// <list type="bullet">
///   <item><c>IX_users_Email</c> — da <c>ownerEmail</c> di <c>/seed/game-night</c>: è il difetto
///     osservato. Guardia nell'handler, che nomina il campo.</item>
///   <item><c>IX_game_night_rsvps_event_user</c> — da <c>userId</c> di <c>/seed/player</c>.
///     Guardia nell'handler, che nomina il campo.</item>
///   <item><c>idx_sessions_code</c> — <b>nessun campo del chiamante</b>: il codice sono 6 cifre
///     esadecimali prese da un Guid dentro <c>SeedTestSessionCommandHandler</c>, quindi 16^6 valori
///     e collisione di compleanno attesa intorno alle 4.800 righe vive. Nessuna guardia può
///     nominare un campo che non esiste: qui la traduzione è l'unica cosa che separa un 409 che
///     nomina il vincolo da un 500 muto.</item>
///   <item><c>IX_UserLibraryEntries_UserId_SharedGameId</c> — <b>non</b> raggiungibile oggi, perché
///     <c>/seed/library-game</c> crea sempre uno SharedGame nuovo. È il tipo di fatto che smette di
///     valere il giorno in cui quell'endpoint accetta un <c>gameId</c>, ed è il motivo per cui la
///     rete non si limita ai vincoli che oggi sanno fallire.</item>
/// </list>
/// <para>
/// Una guardia per vincolo è una lista che invecchia; tradurre lo SQLSTATE copre anche il vincolo
/// aggiunto domani.
/// </para>
/// <para>
/// Il nome del vincolo arriva dai campi d'errore di Postgres (<c>constraint_name</c>,
/// <c>table_name</c>), che Npgsql espone come proprietà di <see cref="PostgresException"/> e che
/// NON sono governati da <c>Include Error Detail</c>: è proprio la parte non redatta, e nomina la
/// colonna meglio di qualunque testo generico.
/// </para>
/// </remarks>
internal static class TestSeedPersistence
{
    /// <summary>SQLSTATE Postgres: <c>unique_violation</c>.</summary>
    internal const string UniqueViolation = "23505";

    /// <summary>SQLSTATE Postgres: <c>foreign_key_violation</c>.</summary>
    internal const string ForeignKeyViolation = "23503";

    /// <summary>
    /// Persiste il batch di un seeder traducendo le violazioni di vincolo in un 4xx che nomina il
    /// vincolo violato. Ogni altro guasto passa invariato (e resta un 500, com'è giusto).
    /// </summary>
    internal static async Task SaveSeedAsync(
        this MeepleAiDbContext db,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pg && IsCallerAttributable(pg.SqlState))
        {
            throw Describe(pg.SqlState, pg.ConstraintName, pg.TableName, ex);
        }
    }

    /// <summary>
    /// Vero solo per i due SQLSTATE che descrivono un conflitto con lo stato del database, non un
    /// guasto. Il filtro sta nella <c>when</c> e non nel corpo del <c>catch</c> perché un
    /// <c>throw ex</c> di ripiego azzererebbe lo stack originale del guasto che NON vogliamo
    /// tradurre.
    /// </summary>
    internal static bool IsCallerAttributable(string? sqlState) =>
        string.Equals(sqlState, UniqueViolation, StringComparison.Ordinal)
        || string.Equals(sqlState, ForeignKeyViolation, StringComparison.Ordinal);

    /// <summary>
    /// Seam testabile: prende i campi d'errore già estratti, così la mappatura si verifica senza
    /// costruire una <see cref="PostgresException"/> reale (le sue proprietà non sono settabili —
    /// stessa separazione di <c>PlayRecordVersionRepository.IsVersionNumberConflict</c>).
    /// </summary>
    internal static HttpException Describe(
        string? sqlState,
        string? constraintName,
        string? tableName,
        Exception? inner)
    {
        var where = $"'{constraintName}' on table '{tableName}'";

        if (string.Equals(sqlState, UniqueViolation, StringComparison.Ordinal))
        {
            // Il messaggio nomina il vincolo e si ferma lì sulle colpe: non tutte le collisioni
            // vengono da un campo del chiamante. `idx_sessions_code` è derivato da un Guid dentro
            // l'handler (6 cifre esadecimali, compleanno intorno alle 4.800 righe): dire «passa un
            // valore nuovo» a chi non ha passato nulla manderebbe l'indagine dalla parte sbagliata.
            var message =
                $"Seed rejected: unique constraint {where} already has a row with these values. " +
                "If the constraint names a field you supplied, pass a fresh value — the seed endpoints " +
                "CREATE rows, they do not reuse existing ones — or POST /api/v1/admin/test/seed/cleanup " +
                "for the leftover testRunId first.";
            return inner is null ? new ConflictException(message) : new ConflictException(message, inner);
        }

        // Resta ForeignKeyViolation: la riga padre non c'è (o non c'è più).
        var fkMessage =
            $"Seed rejected: foreign key {where} points at a row that does not exist. " +
            "Seed the parent entity first and pass the id it returns.";
        return inner is null ? new BadRequestException(fkMessage) : new BadRequestException(fkMessage, inner);
    }
}
