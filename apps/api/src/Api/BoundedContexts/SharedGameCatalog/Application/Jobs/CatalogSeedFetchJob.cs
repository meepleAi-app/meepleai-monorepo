using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Issue #1903 M5: picks <c>Pending</c> <see cref="CatalogSeedDraftEntity"/>
/// entries, invokes <see cref="ICatalogSeedAggregator"/> (Wikidata primary + BGG
/// fallback), persists provenance / raw_payload, and publishes
/// <see cref="CatalogSeedFetchedEvent"/> on success. Runs every 1 minute via
/// Quartz. Batch size 10 with 1s inter-item throttle to respect BGG's ~30 req/min
/// rate limit.
/// </summary>
/// <remarks>
/// <para>Pattern mirror of <c>BackfillPdfCoversJob</c> (issue #1873): same
/// service-provider scoping, per-item try/catch with terminal failure status,
/// and internal <see cref="RunBatchAsync"/> hook for unit tests that drive the
/// batch without spinning up the Quartz scheduler.</para>
/// <para>Per-item failures land the draft in terminal <c>FetchFailed</c> with
/// the exception type name in <c>ErrorMessage</c> (no <c>ex.Message</c> leak —
/// CLAUDE.md memory <c>ex-message-leak-pattern</c>). Operators must manually
/// reset to <c>Pending</c> to retry.</para>
/// </remarks>
[DisallowConcurrentExecution]
public sealed class CatalogSeedFetchJob : IJob
{
    /// <summary>
    /// Maximum number of drafts processed in a single job run. Combined with the
    /// 1-minute trigger and 1s inter-item delay this caps throughput at ~10/min,
    /// safely below BGG's documented ~30 req/min ceiling.
    /// </summary>
    public const int BatchSize = 10;

    /// <summary>
    /// Sleep between individual drafts in a single batch run. Respects BGG XMLAPI2
    /// throttling (spec 2026-06-04-admin-catalog-seed-design.md §7.2).
    /// </summary>
    public const int DelayBetweenItemsMs = 1000;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CatalogSeedFetchJob> _logger;

    public CatalogSeedFetchJob(IServiceProvider serviceProvider, ILogger<CatalogSeedFetchJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        _logger.LogDebug("CatalogSeedFetchJob started: FireTime={FireTime}", context.FireTimeUtc);

        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICatalogSeedDraftRepository>();
        var aggregator = scope.ServiceProvider.GetRequiredService<ICatalogSeedAggregator>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await RunBatchAsync(repo, aggregator, uow, mediator, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal batch runner — extracted from <see cref="Execute"/> so unit tests
    /// can drive it directly without spinning up the Quartz scheduler. Mirrors
    /// <c>BackfillPdfCoversJob.RunBatchAsync</c> from issue #1873.
    /// </summary>
    internal async Task RunBatchAsync(
        ICatalogSeedDraftRepository repo,
        ICatalogSeedAggregator aggregator,
        IUnitOfWork uow,
        IMediator mediator,
        CancellationToken ct)
    {
        var batch = await repo.GetByStatusAsync("Pending", BatchSize, ct).ConfigureAwait(false);
        if (batch.Count == 0)
        {
            _logger.LogDebug("CatalogSeedFetchJob: no Pending drafts in queue");
            return;
        }

        _logger.LogInformation(
            "CatalogSeedFetchJob: picked up {Count} Pending drafts for fetching",
            batch.Count);

        for (var i = 0; i < batch.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var draft = batch[i];
            await ProcessOneAsync(draft, aggregator, uow, mediator, ct).ConfigureAwait(false);

            if (i < batch.Count - 1)
            {
                await Task.Delay(DelayBetweenItemsMs, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessOneAsync(
        CatalogSeedDraftEntity draft,
        ICatalogSeedAggregator aggregator,
        IUnitOfWork uow,
        IMediator mediator,
        CancellationToken ct)
    {
        try
        {
            var query = new CatalogProviderQuery(draft.BggId, draft.WikidataQid, draft.SearchTermInput);
            var result = await aggregator.FetchAsync(query, ct).ConfigureAwait(false);

            if (result.Provenance.Fields.Count == 0)
            {
                draft.Status = "FetchFailed";
                draft.ErrorMessage = $"No provider returned data (providerUsed={result.ProviderUsed})";
                draft.FetchedAt = DateTime.UtcNow;
                _logger.LogWarning(
                    "CatalogSeedFetchJob: no provider data for draft {DraftId} (providerUsed={Provider})",
                    draft.Id, result.ProviderUsed);
            }
            else
            {
                draft.Status = "Fetched";
                draft.ProvenanceJson = result.Provenance.ToJson();
                draft.RawPayloadJson = result.CombinedRawPayload;
                draft.FetchedAt = DateTime.UtcNow;
                draft.ErrorMessage = null;
                _logger.LogInformation(
                    "CatalogSeedFetchJob: fetched draft {DraftId} via {Provider} (fieldCount={Count})",
                    draft.Id, result.ProviderUsed, result.Provenance.Fields.Count);
            }

            await uow.SaveChangesAsync(ct).ConfigureAwait(false);

            // Publish only after successful save — same post-save publish ordering
            // as ApproveCatalogSeedCommandHandler (review-fix H1 pattern, CLAUDE.md
            // pitfall #2620). Avoids triggering downstream side effects for a draft
            // whose Fetched status never persisted.
            if (string.Equals(draft.Status, "Fetched", StringComparison.Ordinal))
            {
                await mediator.Publish(
                    new CatalogSeedFetchedEvent(draft.Id, result.ProviderUsed, result.Provenance.Fields.Count),
                    ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKFILL PATTERN (mirror of BackfillPdfCoversJob): per-item failures
        // must not abort the batch — log, mark the entity FetchFailed, and
        // continue with the next draft so a single pathological provider response
        // doesn't stall the queue. ErrorMessage carries only the exception TYPE
        // (no .Message) per CLAUDE.md memory ex-message-leak-pattern.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex,
                "CatalogSeedFetchJob: unexpected error processing draft {DraftId}",
                draft.Id);
            draft.Status = "FetchFailed";
            var detail = $"{ex.GetType().Name}: see logs";
            draft.ErrorMessage = detail.Length > 500 ? detail[..500] : detail;
            draft.FetchedAt = DateTime.UtcNow;
            try
            {
                await uow.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "CatalogSeedFetchJob: failed to persist FetchFailed status for draft {DraftId}",
                    draft.Id);
            }
        }
    }
}
