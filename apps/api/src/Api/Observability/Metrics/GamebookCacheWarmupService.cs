using System.Diagnostics;
using Api.BoundedContexts.UserLibrary.Application.Queries.Gamebooks;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Observability;

/// <summary>
/// Issue #1292 (AC-6.4): warms up the gamebook index cache for dogfood
/// accounts (<c>IsDemoAccount = true</c>) and superadmins on application
/// startup. Ensures the first <c>GET /api/v1/gamebooks</c> for those users
/// after a deploy is a cache hit (sub-100ms P95).
///
/// Behaviour:
/// <list type="bullet">
///   <item>StartAsync returns immediately; warm-up runs in background</item>
///   <item>30 s grace delay so the DB connection pool is warm and the API
///     is accepting traffic before adding internal load</item>
///   <item>Concurrency capped at 10 simultaneous prefetch operations</item>
///   <item>Per-user errors are logged but never propagated (warm-up is
///     best-effort)</item>
/// </list>
///
/// Mirror convention: <see cref="ProviderQuotaMetricsHostedService"/>.
/// </summary>
internal sealed class GamebookCacheWarmupService : IHostedService
{
    private static readonly TimeSpan GraceDelay = TimeSpan.FromSeconds(30);
    private const int MaxConcurrentPrefetch = 10;

    private readonly IServiceProvider _services;
    private readonly ILogger<GamebookCacheWarmupService> _logger;
    private CancellationTokenSource? _cts;

    public GamebookCacheWarmupService(
        IServiceProvider services,
        ILogger<GamebookCacheWarmupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // AC-6.4: do NOT block IHost.StartAsync. Fire-and-forget background task
        // with linked cancellation token for graceful shutdown.
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(GraceDelay, ct).ConfigureAwait(false);
                await WarmupAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation(ex, "Gamebook cache warm-up cancelled (host shutdown)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gamebook cache warm-up failed");
            }
        }, ct);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        return Task.CompletedTask;
    }

    private async Task WarmupAsync(CancellationToken cancellationToken)
    {
#pragma warning disable MA0004 // CreateAsyncScope returns AsyncServiceScope; ConfigureAwait pattern hides .ServiceProvider. ASP.NET Core has no sync context so the await using is safe.
        await using var scope = _services.CreateAsyncScope();
#pragma warning restore MA0004
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Demo + superadmin users — the dogfood audience.
        var dogfoodUserIds = await db.Users
            .AsNoTracking()
            .Where(u => u.IsDemoAccount || u.Role == "superadmin")
            .Select(u => u.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (dogfoodUserIds.Count == 0)
        {
            _logger.LogInformation("Gamebook cache warm-up: no dogfood users to prefetch");
            return;
        }

        _logger.LogInformation(
            "Gamebook cache warm-up starting for {Count} dogfood users",
            dogfoodUserIds.Count);

        using var semaphore = new SemaphoreSlim(MaxConcurrentPrefetch);
        var totalSw = Stopwatch.StartNew();

        await Parallel.ForEachAsync(
            dogfoodUserIds,
            new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = MaxConcurrentPrefetch },
            async (userId, ct) =>
            {
                await semaphore.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    await PrefetchUserAsync(userId, ct).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ConfigureAwait(false);

        totalSw.Stop();
        _logger.LogInformation(
            "Gamebook cache warm-up completed in {ElapsedMs}ms for {Count} users",
            totalSw.ElapsedMilliseconds, dogfoodUserIds.Count);
    }

    private async Task PrefetchUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Each prefetch uses a fresh scope so MediatR + EF Core scoped services
        // resolve correctly under parallel iteration.
#pragma warning disable MA0004 // CreateAsyncScope returns AsyncServiceScope; ConfigureAwait pattern hides .ServiceProvider. ASP.NET Core has no sync context so the await using is safe.
        await using var scope = _services.CreateAsyncScope();
#pragma warning restore MA0004
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var sw = Stopwatch.StartNew();
        try
        {
            _ = await mediator
                .Send(new GetUserGamebooksQuery(userId), cancellationToken)
                .ConfigureAwait(false);
            sw.Stop();

            MeepleAiMetrics.GamebookCacheWarmupTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "success"));
            MeepleAiMetrics.GamebookCacheWarmupDurationSeconds.Record(sw.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("outcome", "success"));

            _logger.LogInformation(
                "Gamebook cache warm-up for {UserId} completed in {ElapsedMs}ms",
                userId, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // Bubble up — caught by WarmupAsync's outer try.
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            MeepleAiMetrics.GamebookCacheWarmupTotal.Add(1,
                new KeyValuePair<string, object?>("outcome", "failure"));
            MeepleAiMetrics.GamebookCacheWarmupDurationSeconds.Record(sw.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("outcome", "failure"));

            _logger.LogWarning(ex,
                "Gamebook cache warm-up for {UserId} failed after {ElapsedMs}ms",
                userId, sw.ElapsedMilliseconds);
        }
    }
}
