using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Observability;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.BackgroundJobs;

/// <summary>
/// #3383 (ADR-087 D4, task deferito) — ri-ancora periodicamente il gauge
/// <c>meepleai.wikidata.dead_letter_count</c> al <c>COUNT</c> reale sulla tabella degli attempt.
/// <para>
/// Prima di questo servizio il gauge era ibrido: il runner incrementava un contatore in memoria e
/// il <c>WikidataCoverDeadLetterRetentionJob</c> lo ri-ancorava al ground truth una sola volta al
/// giorno (03:00 UTC). A più di un'istanza ogni processo aveva il proprio contatore, quindi
/// <c>sum()</c> raddoppiava e <c>max()</c> derivava. Con un valore puramente DB-derivato ogni
/// istanza riporta lo stesso ground truth e <c>max()</c> torna corretto — vedi l'aggregazione
/// delle regole in <c>infra/prometheus/alerts/wikidata-enrichment.yml</c>.
/// </para>
/// <para>
/// Il gauge continua a leggere il campo in memoria: è QUESTO servizio ad aggiornarlo. Interrogare
/// il DB dentro la callback dell'<c>ObservableGauge</c> — cioè a ogni scrape Prometheus — sarebbe
/// l'anti-pattern che l'issue segnala esplicitamente.
/// </para>
/// </summary>
internal sealed class WikidataDeadLetterMetricsRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WikidataDeadLetterMetricsRefreshService> _logger;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(60);

    public WikidataDeadLetterMetricsRefreshService(
        IServiceScopeFactory scopeFactory,
        ILogger<WikidataDeadLetterMetricsRefreshService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshOnceAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Resilience: un refresh fallito non deve abbattere l'host
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex,
                    "WikidataDeadLetterMetricsRefreshService: refresh fallito; riprovo al prossimo tick");
            }

            try
            {
                await Task.Delay(_refreshInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // shutdown regolare
            }
        }
    }

    /// <summary>Conta i dead-letter e pusha il valore nel gauge. Esposto per i test.</summary>
    public async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var attempts = scope.ServiceProvider
            .GetRequiredService<IWikidataCoverEnrichmentAttemptRepository>();
        var count = await attempts.CountDeadLettersAsync(cancellationToken).ConfigureAwait(false);
        MeepleAiMetrics.SetWikidataDeadLetterCount(count);
    }
}
