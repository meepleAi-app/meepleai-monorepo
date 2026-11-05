using System.Globalization;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>Che cosa ha fatto un giro di pulizia dei caricamenti a pezzi.</summary>
/// <param name="SessionsExpired">Sessioni scadute e mai più richiamate, marcate <c>expired</c>.</param>
/// <param name="DirectoriesDeleted">Cartelle dei pezzi tolte dal disco.</param>
/// <param name="Failures">Sessioni o cartelle che non è stato possibile pulire: il giro successivo riprova.</param>
internal sealed record ChunkedUploadCleanupResult(int SessionsExpired, int DirectoriesDeleted, int Failures);

/// <summary>
/// #4319 (assorbita da #4308): pulizia periodica dei caricamenti a pezzi abbandonati.
/// </summary>
/// <remarks>
/// <para>
/// Un caricamento lasciato a metà non veniva mai ripulito. La scadenza (24 ore) si applicava solo se la sessione
/// veniva richiamata: <c>UploadChunkCommandHandler</c> e <c>CompleteChunkedUploadCommandHandler</c> chiamano
/// <c>MarkAsExpired()</c> quando la ritrovano scaduta. Se nessuno la richiamava, restavano per sempre la riga in
/// <c>chunked_upload_sessions</c> e la cartella dei pezzi sotto <c>meepleai_uploads</c>: frammenti di un file
/// dell'utente, sul disco, senza scadenza. <c>FindExpiredSessionsAsync</c> esisteva e non aveva chiamanti.
/// </para>
/// <para>Un giro fa due passate.</para>
/// <list type="number">
/// <item>
/// <b>Le sessioni scadute</b>: si marca la riga <c>expired</c> e poi si toglie la cartella, subito. Una sessione scaduta
/// rifiuta ogni pezzo (<c>MarkChunkReceived</c>), quindi non c'è più nessuno che scriva. Fa eccezione
/// <c>assembling</c>: <c>CompleteChunkedUploadCommandHandler</c> controlla la scadenza una volta, prima di cominciare,
/// e poi legge i pezzi da quella cartella per tutto l'assemblaggio, che può durare oltre il minuto in cui
/// <c>ExpiresAt</c> passa. Una sessione in assemblaggio si chiude solo quando è scaduta da più di una sessione, cioè
/// quando l'assemblaggio è stato interrotto da un riavvio.
/// </item>
/// <item>
/// <b>Le cartelle sotto la base</b> che nessuna sessione viva reclama: si tolgono solo se la loro ultima scrittura è
/// più vecchia di quanto una sessione possa vivere. Servono tre casi. Una sessione già <c>expired</c> senza
/// cartella tolta (<c>UploadChunkCommandHandler</c> marca e non cancella). Una cartella senza riga: la cancellazione
/// di un gioco privato (#4244) toglie le sessioni aperte ma non le cartelle. E il margine d'età è dovuto a
/// <c>InitChunkedUploadCommandHandler</c>, che crea la cartella <i>prima</i> di inserire la riga: una cartella
/// appena nata senza riga è un caricamento che sta partendo.
/// </item>
/// </list>
/// <para>
/// Le sessioni si leggono dal <c>DbContext</c> con una proiezione (<c>Id</c>, <c>Status</c>, <c>TempDirectory</c>) e si
/// aggiornano con <c>ExecuteUpdateAsync</c> condizionato allo stato letto, senza passare dall'aggregato. Due ragioni.
/// La chiave esterna <c>GameId</c> è <c>ON DELETE SET NULL</c>: se un gioco viene cancellato fisicamente la sessione
/// resta senza <c>GameId</c> e senza <c>PrivateGameId</c>, e <c>ChunkedUploadSession</c> non si lascia costruire così,
/// quindi <c>FindExpiredSessionsAsync</c> lancia e il giro si ferma su quella riga per sempre (misurato nei test). E
/// <c>UpdateAsync</c> riscrive la riga intera da uno snapshot: senza concorrenza ottimistica avrebbe potuto
/// sovrascrivere un <c>completed</c> appena scritto. Con lo stato nella condizione, se qualcun altro ha mosso la
/// sessione nel frattempo l'aggiornamento non colpisce niente e la sessione si lascia stare.
/// </para>
/// <para>
/// Il percorso di ogni cartella viene dal database. Si cancella solo un <i>figlio diretto</i> della cartella di base:
/// i gestori producono sempre e solo <c>&lt;base&gt;/&lt;guid&gt;</c>. Non la base stessa, non una sorella con lo stesso
/// prefisso, non una cartella annidata (un collegamento simbolico intermedio non verrebbe visto), e un collegamento
/// simbolico non si segue. Il controllo è più stretto di quello dei tre gestori, che confrontano solo il prefisso
/// della stringa: per leggere o scrivere un pezzo basta, per cancellare no.
/// </para>
/// <para>
/// Con più repliche dell'API ogni replica vede solo il proprio disco: la riga la marca scaduta la prima che passa,
/// e la cartella sull'altra replica la toglie la passata sulle cartelle, perché la riga non è più viva.
/// </para>
/// </remarks>
internal sealed class AbandonedChunkedUploadCleanupService : BackgroundService
{
    /// <summary>Stessa cartella di base dei tre gestori dei caricamenti a pezzi.</summary>
    internal static readonly string DefaultUploadBasePath = Path.Combine(Path.GetTempPath(), "meepleai_uploads");

    internal const string IntervalMinutesKey = "ChunkedUploadCleanup:IntervalMinutes";
    internal const int DefaultIntervalMinutes = 60;

    /// <summary>
    /// Un giorno: <c>PeriodicTimer</c> lancia oltre circa cinquanta giorni, e una pulizia più rada di una sessione non
    /// ha senso.
    /// </summary>
    internal const int MaxIntervalMinutes = 24 * 60;

    private const string AssemblingStatus = "assembling";
    private const string ExpiredStatus = "expired";

    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Quanto deve essere vecchia l'ultima scrittura di una cartella senza sessione viva prima di toglierla, e da
    /// quanto deve essere scaduta una sessione in assemblaggio prima di chiuderla: quanto può vivere una sessione.
    /// </summary>
    private static readonly TimeSpan OrphanGrace = TimeSpan.FromHours(ChunkedUploadSession.SessionExpirationHours);

    private static readonly string[] TerminalStatuses = ["completed", "failed", ExpiredStatus];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AbandonedChunkedUploadCleanupService> _logger;
    private readonly string _uploadBasePath;

    public AbandonedChunkedUploadCleanupService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AbandonedChunkedUploadCleanupService> logger,
        string? uploadBasePath = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _uploadBasePath = NormalizePath(uploadBasePath ?? DefaultUploadBasePath);
    }

    /// <summary>
    /// L'intervallo tra due giri, in minuti: 60 se la chiave manca, non è un intero o è minore di uno; al più un giorno.
    /// Fuori dal giro protetto un valore che fa lanciare <c>PeriodicTimer</c> (zero, negativo, enorme) o
    /// <c>GetValue&lt;int&gt;</c> (non è un intero) farebbe morire il servizio in silenzio dopo il ritardo iniziale.
    /// </summary>
    internal static TimeSpan ResolveInterval(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var parsed = int.TryParse(
            configuration[IntervalMinutesKey], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes);

        return parsed && minutes >= 1
            ? TimeSpan.FromMinutes(Math.Min(minutes, MaxIntervalMinutes))
            : TimeSpan.FromMinutes(DefaultIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var interval = ResolveInterval(_configuration);
        _logger.LogInformation(
            "[ChunkedUploadCleanup] Started. Interval: {Interval} minute(s), base folder: {BasePath}",
            interval.TotalMinutes, _uploadBasePath);

        using var timer = new PeriodicTimer(interval);
        do
        {
            await RunGuardedAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken).ConfigureAwait(false));
    }

    private async Task RunGuardedAsync(CancellationToken cancellationToken)
    {
#pragma warning disable CA1031 // Background service boundary: a failed round must not crash the host; the next tick retries
        try
        {
            var result = await CleanupOnceAsync(cancellationToken).ConfigureAwait(false);
            if (result.SessionsExpired > 0 || result.DirectoriesDeleted > 0 || result.Failures > 0)
            {
                _logger.LogInformation(
                    "[ChunkedUploadCleanup] Expired {Sessions} session(s), deleted {Folders} folder(s), {Failures} failure(s)",
                    result.SessionsExpired, result.DirectoriesDeleted, result.Failures);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[ChunkedUploadCleanup] Cleanup round failed");
        }
#pragma warning restore CA1031
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Un giro di pulizia. Esposto <c>internal</c> per i test d'integrazione (InternalsVisibleTo Api.Tests).
    /// </summary>
    internal async Task<ChunkedUploadCleanupResult> CleanupOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var now = DateTime.UtcNow;
        var tally = new Tally();

        await ExpireSessionsAsync(db, now, tally, cancellationToken).ConfigureAwait(false);
        await SweepFoldersAsync(db, now, tally, cancellationToken).ConfigureAwait(false);

        return new ChunkedUploadCleanupResult(tally.Expired, tally.Deleted, tally.Failures);
    }

    /// <summary>
    /// Prima passata: le sessioni scadute. Si marca la riga e poi si toglie la cartella, in questo ordine: se la
    /// cartella non si lascia togliere la riga è già a posto, e la seconda passata riprova quando sarà vecchia.
    /// </summary>
    private async Task ExpireSessionsAsync(
        MeepleAiDbContext db, DateTime now, Tally tally, CancellationToken cancellationToken)
    {
        var assemblingCutoff = now - OrphanGrace;

        var candidates = await db.ChunkedUploadSessions
            .AsNoTracking()
            .Where(s => s.ExpiresAt < now
                        && !TerminalStatuses.Contains(s.Status)
                        && (s.Status != AssemblingStatus || s.ExpiresAt < assemblingCutoff))
            .Select(s => new { s.Id, s.Status, s.TempDirectory })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await TryMarkExpiredAsync(db, candidate.Id, candidate.Status, now, tally, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

            tally.Add(TryDeleteFolder(candidate.TempDirectory));
        }
    }

    /// <summary>
    /// Marca la sessione scaduta se è ancora nello stato in cui è stata letta. Falso se qualcun altro l'ha mossa nel
    /// frattempo (un completamento appena scritto vince) o se l'aggiornamento è fallito.
    /// </summary>
    private async Task<bool> TryMarkExpiredAsync(
        MeepleAiDbContext db, Guid sessionId, string statusRead, DateTime now, Tally tally,
        CancellationToken cancellationToken)
    {
#pragma warning disable CA1031 // Per-session boundary: one bad row must not stop the rest of the round
        try
        {
            var updated = await db.ChunkedUploadSessions
                .Where(s => s.Id == sessionId && s.Status == statusRead)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(s => s.Status, ExpiredStatus)
                        .SetProperty(s => s.CompletedAt, (DateTime?)now),
                    cancellationToken)
                .ConfigureAwait(false);

            if (updated == 0)
            {
                return false;
            }

            tally.Expired++;
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            tally.Failures++;
            _logger.LogWarning(ex, "[ChunkedUploadCleanup] Could not expire session {SessionId}", sessionId);
            return false;
        }
#pragma warning restore CA1031
    }

    /// <summary>Seconda passata: le cartelle sotto la base che nessuna sessione viva reclama.</summary>
    private async Task SweepFoldersAsync(
        MeepleAiDbContext db, DateTime now, Tally tally, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_uploadBasePath))
        {
            return;
        }

        var liveFolders = await LoadLiveFoldersAsync(db, now, cancellationToken).ConfigureAwait(false);

        foreach (var folder in Directory.EnumerateDirectories(_uploadBasePath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new DirectoryInfo(folder);
            if (liveFolders.Contains(NormalizePath(info.FullName)) || now - info.LastWriteTimeUtc < OrphanGrace)
            {
                continue;
            }

            tally.Add(TryDeleteFolder(info.FullName));
        }
    }

    /// <summary>Le cartelle che una sessione non scaduta e non conclusa sta ancora usando.</summary>
    private static async Task<HashSet<string>> LoadLiveFoldersAsync(
        MeepleAiDbContext db, DateTime now, CancellationToken cancellationToken)
    {
        var tempDirectories = await db.ChunkedUploadSessions
            .AsNoTracking()
            .Where(s => !TerminalStatuses.Contains(s.Status) && s.ExpiresAt >= now)
            .Select(s => s.TempDirectory)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var live = new HashSet<string>(PathComparer);
        foreach (var tempDirectory in tempDirectories)
        {
            if (TryNormalizePath(tempDirectory, out var normalized))
            {
                live.Add(normalized);
            }
        }

        return live;
    }

    private enum FolderOutcome
    {
        /// <summary>La cartella non c'era, o non è nostra da togliere: niente da fare.</summary>
        Skipped,
        Deleted,
        Failed,
    }

    /// <summary>I conteggi di un giro, passati alle due passate.</summary>
    private sealed class Tally
    {
        public int Expired { get; set; }

        public int Deleted { get; private set; }

        public int Failures { get; set; }

        public void Add(FolderOutcome outcome)
        {
            switch (outcome)
            {
                case FolderOutcome.Deleted:
                    Deleted++;
                    break;
                case FolderOutcome.Failed:
                    Failures++;
                    break;
            }
        }
    }

    private FolderOutcome TryDeleteFolder(string? tempDirectory)
    {
        if (!IsDirectChildOfUploadBase(tempDirectory, out var fullPath))
        {
            _logger.LogWarning(
                "[ChunkedUploadCleanup] Refusing to delete {TempDirectory}: not a direct child of {BasePath}",
                tempDirectory, _uploadBasePath);
            return FolderOutcome.Skipped;
        }

        var info = new DirectoryInfo(fullPath);
        if (!info.Exists)
        {
            return FolderOutcome.Skipped;
        }

        // Un collegamento simbolico o una giunzione dentro la base non si segue e non si cancella.
        if (info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            _logger.LogWarning("[ChunkedUploadCleanup] Skipping {Path}: it is a link", fullPath);
            return FolderOutcome.Skipped;
        }

        try
        {
            info.Delete(recursive: true);
            return FolderOutcome.Deleted;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "[ChunkedUploadCleanup] Could not delete {Path}; the next round retries", fullPath);
            return FolderOutcome.Failed;
        }
    }

    /// <summary>
    /// Vero solo se il percorso è un figlio <i>diretto</i> della cartella di base, che è la forma che producono i
    /// gestori (<c>&lt;base&gt;/&lt;guid&gt;</c>). Non la base stessa (cancellerebbe i caricamenti di tutti), non una
    /// sorella che ne condivide il prefisso (<c>meepleai_uploads_altro</c>), non una cartella annidata.
    /// </summary>
    private bool IsDirectChildOfUploadBase(string? tempDirectory, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(tempDirectory) ||
            tempDirectory.Contains("..", StringComparison.Ordinal) ||
            !TryNormalizePath(tempDirectory, out var normalized))
        {
            return false;
        }

        var parent = Path.GetDirectoryName(normalized);
        if (parent is null || !string.Equals(parent, _uploadBasePath, PathComparison))
        {
            return false;
        }

        fullPath = normalized;
        return true;
    }

    private static bool TryNormalizePath(string path, out string normalized)
    {
        try
        {
            normalized = NormalizePath(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            normalized = string.Empty;
            return false;
        }
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
