using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
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
/// <b>Le sessioni scadute</b> (<c>FindExpiredSessionsAsync</c>): si marcano <c>expired</c> e si toglie la cartella,
/// subito. Una sessione scaduta rifiuta ogni pezzo (<c>MarkChunkReceived</c>), quindi non c'è più nessuno che scriva.
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
/// Il percorso di ogni cartella viene dal database. Non si cancella mai niente che non stia <i>strettamente dentro</i>
/// la cartella di base (non la base stessa, non una sorella con lo stesso prefisso), e un collegamento simbolico non
/// si segue. Il controllo è più stretto di quello dei due gestori, che confrontano solo il prefisso della stringa:
/// per leggere o scrivere un pezzo basta, per cancellare no.
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

    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Quanto deve essere vecchia l'ultima scrittura di una cartella senza sessione viva prima di toglierla: quanto
    /// può vivere una sessione, più il tempo di una richiesta in volo.
    /// </summary>
    private static readonly TimeSpan OrphanGrace = TimeSpan.FromHours(ChunkedUploadSession.SessionExpirationHours);

    private static readonly string[] TerminalStatuses = ["completed", "failed", "expired"];

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

    private TimeSpan Interval =>
        TimeSpan.FromMinutes(_configuration.GetValue("ChunkedUploadCleanup:IntervalMinutes", 60));

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

        var interval = Interval;
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
        var sessions = scope.ServiceProvider.GetRequiredService<IChunkedUploadSessionRepository>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var expired = 0;
        var deleted = 0;
        var failures = 0;

        // Prima passata: le sessioni scadute. Si marca la riga e poi si toglie la cartella, in questo ordine: se la
        // cartella non si lascia togliere la riga è già a posto, e la seconda passata riprova quando sarà vecchia.
        var expiredSessions = await sessions.FindExpiredSessionsAsync(cancellationToken).ConfigureAwait(false);
        foreach (var session in expiredSessions)
        {
            cancellationToken.ThrowIfCancellationRequested();

#pragma warning disable CA1031 // Per-session boundary: one bad row must not stop the rest of the round
            try
            {
                session.MarkAsExpired();
                await sessions.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                expired++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures++;
                _logger.LogWarning(ex, "[ChunkedUploadCleanup] Could not expire session {SessionId}", session.Id);
                continue;
            }
#pragma warning restore CA1031

            switch (TryDeleteFolder(session.TempDirectory))
            {
                case FolderOutcome.Deleted:
                    deleted++;
                    break;
                case FolderOutcome.Failed:
                    failures++;
                    break;
            }
        }

        // Seconda passata: le cartelle sotto la base che nessuna sessione viva reclama.
        if (Directory.Exists(_uploadBasePath))
        {
            var now = DateTime.UtcNow;
            var liveFolders = await LoadLiveFoldersAsync(db, now, cancellationToken).ConfigureAwait(false);

            foreach (var folder in Directory.EnumerateDirectories(_uploadBasePath))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var info = new DirectoryInfo(folder);
                if (liveFolders.Contains(NormalizePath(info.FullName)) ||
                    now - info.LastWriteTimeUtc < OrphanGrace)
                {
                    continue;
                }

                switch (TryDeleteFolder(info.FullName))
                {
                    case FolderOutcome.Deleted:
                        deleted++;
                        break;
                    case FolderOutcome.Failed:
                        failures++;
                        break;
                }
            }
        }

        return new ChunkedUploadCleanupResult(expired, deleted, failures);
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

    private FolderOutcome TryDeleteFolder(string? tempDirectory)
    {
        if (!IsStrictlyInsideUploadBase(tempDirectory, out var fullPath))
        {
            _logger.LogWarning(
                "[ChunkedUploadCleanup] Refusing to delete {TempDirectory}: not strictly inside {BasePath}",
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
    /// Vero solo se il percorso sta <i>dentro</i> la cartella di base: non la base stessa (cancellerebbe i caricamenti
    /// di tutti) e non una sorella che ne condivide il prefisso (<c>meepleai_uploads_altro</c>).
    /// </summary>
    private bool IsStrictlyInsideUploadBase(string? tempDirectory, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(tempDirectory) ||
            tempDirectory.Contains("..", StringComparison.Ordinal) ||
            !TryNormalizePath(tempDirectory, out var normalized))
        {
            return false;
        }

        if (!normalized.StartsWith(_uploadBasePath + Path.DirectorySeparatorChar, PathComparison))
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
