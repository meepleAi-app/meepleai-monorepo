using System.Diagnostics;
using Api.Observability;
using Polly;
using Polly.Retry;

namespace Api.Services;

/// <summary>
/// Polly v8 ResiliencePipeline-backed implementation of
/// <see cref="ICacheInvalidationRetryPolicy"/>.
///
/// Configuration (Issue #613):
/// <list type="bullet">
///   <item>Max 3 retry attempts (4 total invocations).</item>
///   <item>Exponential backoff with base 200ms and factor 2 (≈200ms, 400ms, 800ms),
///   jittered. Total worst-case time stays under ~4s including jitter.</item>
///   <item>Retries on any <see cref="Exception"/> EXCEPT
///   <see cref="OperationCanceledException"/> when the caller token is cancelled,
///   <see cref="ArgumentException"/> family, and <see cref="ObjectDisposedException"/>
///   — those represent programming errors or deliberate cancellation.</item>
/// </list>
/// </summary>
internal sealed class CacheInvalidationRetryPolicy : ICacheInvalidationRetryPolicy
{
    private readonly ILogger<CacheInvalidationRetryPolicy> _logger;
    private readonly ResiliencePipeline _pipeline;

    public CacheInvalidationRetryPolicy(ILogger<CacheInvalidationRetryPolicy> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex =>
                        ex is not ArgumentException
                        && ex is not ObjectDisposedException),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = true,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Cache invalidation retry {Attempt}/3 after {Delay}ms for operation",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds);
                    MeepleAiMetrics.RecordCacheInvalidationRetry();
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, ValueTask> operation,
        string operationName,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrEmpty(operationName);

        var sw = Stopwatch.StartNew();
        try
        {
            await _pipeline.ExecuteAsync(
                async token => await operation(token).ConfigureAwait(false),
                ct).ConfigureAwait(false);
            MeepleAiMetrics.RecordCacheInvalidationOutcome(operationName, "success");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller cancelled — neutral outcome, do not flood metrics with failures.
            throw;
        }
        catch (Exception ex)
        {
            MeepleAiMetrics.RecordCacheInvalidationOutcome(operationName, "failure");
            _logger.LogError(
                ex,
                "Cache invalidation '{OperationName}' permanently failed after retries in {ElapsedMs}ms",
                operationName,
                sw.ElapsedMilliseconds);
            throw;
        }
    }
}
