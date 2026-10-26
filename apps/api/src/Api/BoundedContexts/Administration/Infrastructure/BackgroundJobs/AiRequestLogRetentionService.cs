using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.BackgroundJobs;

/// <summary>
/// #4311 (assorbita da #4308): scadenza dei testi di <c>ai_request_logs</c>.
/// </summary>
/// <remarks>
/// <para>
/// La tabella conserva, per ogni richiesta all'AI, la domanda (<c>Query</c>), fino a 1024 caratteri della risposta
/// (<c>ResponseSnippet</c>) e il testo dei passaggi recuperati (<c>ChunksJson</c>: <c>{id, score, text, page,
/// chunkIndex, pdfName, used}</c>). Nel codice dell'API non c'era nessuna cancellazione di quelle righe. Sono
/// contenuto derivato dai PDF, anche da quelli di un gioco privato che l'utente ha poi cancellato (#4244), e le
/// pagine di amministrazione lo leggono.
/// </para>
/// <para>
/// Decisione (2026-10-25): una scadenza per tutti, che <b>svuota i testi e tiene le metriche</b>. Le righe più
/// vecchie di <c>Administration:AiRequestLogRetentionDays</c> giorni (90 se assente: la stessa finestra delle viste
/// admin sugli eventi) perdono <c>Query</c>, <c>ResponseSnippet</c> e <c>ChunksJson</c>; la riga resta, con
/// l'utente, il gioco, i token, le latenze, le confidenze e il <c>BreakdownJson</c> (solo millisecondi). Le
/// statistiche degli amministratori lavorano su quelle colonne, non sui testi.
/// </para>
/// <para>
/// <c>ChunksJson</c> non era nominata nell'issue, che parlava dei due testi più piccoli. È però la copia più grande
/// di contenuto del PDF in questa tabella, senza il limite dei 1024 caratteri: svuotarne solo due su tre avrebbe
/// lasciato la parte principale.
/// </para>
/// <para>
/// Una scadenza di zero giorni o negativa non vuol dire «svuota tutto»: è un'impostazione sbagliata, e il servizio
/// non tocca niente. Le righe si svuotano a lotti (<see cref="DefaultBatchSize"/>) per non tenere una scrittura
/// lunga sulla tabella. L'operazione è ripetibile: una riga già svuotata non si riscrive.
/// </para>
/// </remarks>
internal sealed class AiRequestLogRetentionService : BackgroundService
{
    internal const string RetentionDaysKey = "Administration:AiRequestLogRetentionDays";
    internal const int DefaultRetentionDays = 90;
    internal const int DefaultBatchSize = 5_000;

    private static readonly TimeSpan InitialDelay = TimeSpan.FromHours(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiRequestLogRetentionService> _logger;
    private readonly int _batchSize;

    public AiRequestLogRetentionService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AiRequestLogRetentionService> logger,
        int batchSize = DefaultBatchSize)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _batchSize = batchSize > 0 ? batchSize : DefaultBatchSize;
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

        using var timer = new PeriodicTimer(Interval);
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
            await PurgeOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[AiRequestLogRetention] Purge round failed");
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
    /// Un giro: svuota i testi delle righe più vecchie della scadenza e restituisce quante ne ha svuotate. Esposto
    /// <c>internal</c> per i test d'integrazione (InternalsVisibleTo Api.Tests).
    /// </summary>
    internal async Task<int> PurgeOnceAsync(CancellationToken cancellationToken)
    {
        var days = _configuration.GetValue<int?>(RetentionDaysKey) ?? DefaultRetentionDays;
        if (days <= 0)
        {
            _logger.LogWarning(
                "[AiRequestLogRetention] {Key}={Days} is not a positive number of days: nothing is purged",
                RetentionDaysKey, days);
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddDays(-days);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var total = 0;

        // Paginazione per chiave: ogni lotto riparte dopo l'ultimo id visto. Il ciclo finisce comunque, anche se un
        // domani il filtro e l'aggiornamento non coincidessero (una colonna aggiunta a uno e non all'altro) e una
        // riga restasse selezionabile dopo l'aggiornamento: ripartendo sempre dall'inizio, il servizio
        // riscriverebbe le stesse righe per sempre. Trovato perturbando il codice nei test.
        var lastId = Guid.Empty;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Solo le righe che hanno ancora qualcosa da svuotare: una riga già svuotata non si riscrive, e il
            // conteggio dice quanto si è fatto davvero.
            var ids = await db.AiRequestLogs
                .AsNoTracking()
                .Where(l => l.Id > lastId
                            && l.CreatedAt < cutoff
                            && (l.Query != null || l.ResponseSnippet != null || l.ChunksJson != null))
                .OrderBy(l => l.Id)
                .Select(l => l.Id)
                .Take(_batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (ids.Count == 0)
            {
                break;
            }

            lastId = ids[^1];

            var blanked = await db.AiRequestLogs
                .Where(l => ids.Contains(l.Id))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(l => l.Query, (string?)null)
                        .SetProperty(l => l.ResponseSnippet, (string?)null)
                        .SetProperty(l => l.ChunksJson, (string?)null),
                    cancellationToken)
                .ConfigureAwait(false);

            total += blanked;

            // Un lotto parziale è l'ultimo.
            if (ids.Count < _batchSize)
            {
                break;
            }
        }

        if (total > 0)
        {
            _logger.LogInformation(
                "[AiRequestLogRetention] Blanked the texts of {Count} request log(s) older than {Cutoff:O} ({Days} days)",
                total, cutoff, days);
        }

        return total;
    }
}
