using System.Security.Cryptography;
using System.Text;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Issue #1903 M7.1 — monthly watcher that fetches the BoardGameGeek terms of
/// service page, hashes the body with SHA-256, and compares the result against
/// the persisted <see cref="BggTosHashEntity"/> singleton row. On change, logs
/// a warning so operators can perform the manual legal review required by spec
/// §8.5.6 (pre-rollout legal checklist).
/// </summary>
/// <remarks>
/// <para>Pattern follows <c>CatalogSeedFetchJob</c>: <see cref="IServiceProvider"/>
/// scoped per execution, <see cref="DisallowConcurrentExecutionAttribute"/> to
/// avoid double-fires, internal <see cref="RunOnceAsync"/> hook for unit tests
/// that drive the watcher directly without spinning up the Quartz scheduler.</para>
/// <para>Network/HTTP failures are caught and logged at <c>Warning</c> level
/// (the watcher is best-effort — a single missed run is acceptable); the only
/// exception that propagates is <see cref="OperationCanceledException"/> when
/// shutdown is requested. Error messages never include <c>ex.Message</c> per
/// CLAUDE.md memory <c>ex-message-leak-pattern</c>.</para>
/// <para><b>IMPORTANT — runs regardless of feature flag:</b> this job is NOT
/// gated by the <c>AdminCatalogSeedEnabled</c> runtime flag. The watcher is
/// the compliance audit trail mandated by spec §8.5.6 — operators must have
/// a continuous record of ToS changes BEFORE enabling the pipeline so they
/// can perform the pre-rollout legal review with full context. Gating the
/// watcher by the flag would silently miss ToS changes during the staging /
/// pre-rollout window, defeating the purpose of the audit trail (M7 review
/// fix).</para>
/// </remarks>
[DisallowConcurrentExecution]
public sealed class BggTosWatcherJob : IJob
{
    /// <summary>
    /// Public URL of the BGG terms of service page. S1075 suppressed: stable
    /// third-party service URL.
    /// </summary>
#pragma warning disable S1075 // URIs should not be hardcoded
    public const string BggTosUrl = "https://boardgamegeek.com/terms";
#pragma warning restore S1075

    /// <summary>
    /// HTTP timeout for a single ToS fetch. Generous — BGG occasionally takes
    /// 10s+ to serve static pages, and the watcher only runs monthly.
    /// </summary>
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BggTosWatcherJob> _logger;

    public BggTosWatcherJob(IServiceProvider serviceProvider, ILogger<BggTosWatcherJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        _logger.LogDebug("BggTosWatcherJob started: FireTime={FireTime}", context.FireTimeUtc);

        using var scope = _serviceProvider.CreateScope();

        // NOTE: this job intentionally runs regardless of the
        // AdminCatalogSeedEnabled feature flag — see class-level <remarks>
        // (compliance audit trail per spec §8.5.6). Do NOT add a flag check
        // here; gating the watcher would silently miss ToS changes during
        // the pre-rollout window (M7 review fix).

        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        await RunOnceAsync(db, httpFactory, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Internal single-shot runner — extracted from <see cref="Execute"/> so
    /// unit tests can drive it directly without the Quartz scheduler.
    /// </summary>
    internal async Task RunOnceAsync(
        MeepleAiDbContext db,
        IHttpClientFactory httpFactory,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(httpFactory);

        string html;
        try
        {
            using var client = httpFactory.CreateClient();
            client.Timeout = HttpTimeout;
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MeepleAI/1.0 (admin-catalog-seed-tos-watcher; abuse@meepleai.app)");

            html = await client.GetStringAsync(new Uri(BggTosUrl), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKFILL PATTERN: a single HTTP failure must not crash the scheduler.
        // Log the exception type (no .Message — CLAUDE.md ex-message-leak-pattern)
        // and let the next monthly run retry.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex,
                "BggTosWatcherJob: failed to fetch BGG ToS at {Url} (errorType={ErrorType})",
                BggTosUrl, ex.GetType().Name);
            return;
        }

        var hash = ComputeSha256Hex(html);
        var now = DateTime.UtcNow;

        var row = await db.BggTosHashes
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == BggTosHashEntity.SingletonId, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new BggTosHashEntity
            {
                Id = BggTosHashEntity.SingletonId,
                CurrentHash = hash,
                LastCheckedAt = now,
                LastChangedAt = null,
                ChangeCount = 0,
            };
            db.BggTosHashes.Add(row);
            _logger.LogInformation(
                "BggTosWatcherJob: initial BGG ToS hash recorded ({Hash})",
                hash);
        }
        else if (!string.Equals(row.CurrentHash, hash, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "BggTosWatcherJob: BGG ToS HASH CHANGED. Old={OldHash} New={NewHash}. " +
                "MANUAL LEGAL REVIEW REQUIRED — see spec §8.5.6 pre-rollout checklist.",
                row.CurrentHash, hash);
            row.CurrentHash = hash;
            row.LastChangedAt = now;
            row.ChangeCount++;
        }
        else
        {
            _logger.LogDebug(
                "BggTosWatcherJob: BGG ToS hash unchanged ({Hash})", hash);
        }

        row.LastCheckedAt = now;

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex,
                "BggTosWatcherJob: failed to persist BGG ToS hash row (errorType={ErrorType})",
                ex.GetType().Name);
        }
    }

    /// <summary>
    /// Computes a lowercase hexadecimal SHA-256 digest of <paramref name="content"/>
    /// (UTF-8 encoded). Result is always 64 characters.
    /// </summary>
    internal static string ComputeSha256Hex(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
