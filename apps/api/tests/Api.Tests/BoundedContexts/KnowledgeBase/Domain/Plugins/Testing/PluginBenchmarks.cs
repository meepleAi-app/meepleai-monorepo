// =============================================================================
// MeepleAI - RAG Plugin System
// Issue #3430 - Plugin Testing Framework
// =============================================================================

using System.Diagnostics;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Testing;

/// <summary>
/// Performance benchmark utilities for plugin testing.
/// Provides methods for measuring and analyzing plugin performance characteristics.
/// </summary>
public static class PluginBenchmarks
{
    /// <summary>
    /// Runs a performance benchmark on a plugin.
    /// </summary>
    /// <param name="plugin">The plugin to benchmark.</param>
    /// <param name="inputGenerator">Function to generate test inputs.</param>
    /// <param name="config">Plugin configuration to use.</param>
    /// <param name="options">Benchmark options.</param>
    /// <returns>Benchmark results.</returns>
    public static async Task<BenchmarkResult> RunBenchmarkAsync(
        IRagPlugin plugin,
        Func<PluginInput> inputGenerator,
        PluginConfig? config = null,
        BenchmarkOptions? options = null)
    {
        options ??= BenchmarkOptions.Default;
        config ??= PluginConfig.Default();

        var results = new BenchmarkResult
        {
            PluginId = plugin.Id,
            PluginVersion = plugin.Version,
            Configuration = config,
            Options = options,
            StartTime = DateTimeOffset.UtcNow
        };

        // Warmup phase
        for (var i = 0; i < options.WarmupIterations; i++)
        {
            var warmupInput = inputGenerator();
            await plugin.ExecuteAsync(warmupInput, config).ConfigureAwait(false);
        }

        // Measurement phase
        var durations = new List<double>(options.Iterations);
        var successCount = 0;
        var failureCount = 0;
        long totalMemoryBefore = 0;
        long totalMemoryAfter = 0;

        // Force GC before benchmarking
        if (options.MeasureMemory)
        {
#pragma warning disable S1215 // GC.Collect is intentional for benchmarking memory measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            totalMemoryBefore = GC.GetTotalMemory(true);
#pragma warning restore S1215
        }

        var stopwatch = new Stopwatch();

        for (var i = 0; i < options.Iterations; i++)
        {
            var input = inputGenerator();

            stopwatch.Restart();
            var output = await plugin.ExecuteAsync(input, config).ConfigureAwait(false);
            stopwatch.Stop();

            durations.Add(stopwatch.Elapsed.TotalMilliseconds);

            if (output.Success)
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }

            // Optional delay between iterations
            if (options.DelayBetweenIterationsMs > 0)
            {
                await Task.Delay(options.DelayBetweenIterationsMs).ConfigureAwait(false);
            }
        }

        if (options.MeasureMemory)
        {
#pragma warning disable S1215 // GC.Collect is intentional for benchmarking memory measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            totalMemoryAfter = GC.GetTotalMemory(true);
#pragma warning restore S1215
        }

        results.EndTime = DateTimeOffset.UtcNow;
        results.SuccessCount = successCount;
        results.FailureCount = failureCount;
        results.Durations = durations;

        if (options.MeasureMemory)
        {
            results.MemoryDelta = totalMemoryAfter - totalMemoryBefore;
        }

        return results;
    }

    /// <summary>
    /// Runs a throughput benchmark measuring operations per second.
    /// </summary>
    /// <param name="plugin">The plugin to benchmark.</param>
    /// <param name="inputGenerator">Function to generate test inputs.</param>
    /// <param name="config">Plugin configuration to use.</param>
    /// <param name="durationSeconds">How long to run the benchmark.</param>
    /// <returns>Throughput results.</returns>
    public static async Task<ThroughputResult> RunThroughputBenchmarkAsync(
        IRagPlugin plugin,
        Func<PluginInput> inputGenerator,
        PluginConfig? config = null,
        int durationSeconds = 10)
    {
        config ??= PluginConfig.Default();

        // Warmup
        for (var i = 0; i < 3; i++)
        {
            await plugin.ExecuteAsync(inputGenerator(), config).ConfigureAwait(false);
        }

        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.AddSeconds(durationSeconds);
        var operationCount = 0;
        var successCount = 0;
        var stopwatch = Stopwatch.StartNew();

        while (DateTimeOffset.UtcNow < endTime)
        {
            var input = inputGenerator();
            var output = await plugin.ExecuteAsync(input, config).ConfigureAwait(false);

            operationCount++;
            if (output.Success)
            {
                successCount++;
            }
        }

        stopwatch.Stop();

        return new ThroughputResult
        {
            PluginId = plugin.Id,
            TotalOperations = operationCount,
            SuccessfulOperations = successCount,
            TotalDurationMs = stopwatch.Elapsed.TotalMilliseconds,
            OperationsPerSecond = operationCount / (stopwatch.Elapsed.TotalMilliseconds / 1000.0),
            SuccessRate = operationCount > 0 ? (double)successCount / operationCount : 0
        };
    }

    /// <summary>
    /// Runs a concurrent execution benchmark.
    /// </summary>
    /// <param name="plugin">The plugin to benchmark.</param>
    /// <param name="inputGenerator">Function to generate test inputs.</param>
    /// <param name="config">Plugin configuration to use.</param>
    /// <param name="concurrency">Number of concurrent operations.</param>
    /// <param name="operationsPerTask">Number of operations each task should perform.</param>
    /// <returns>Concurrency benchmark results.</returns>
    public static async Task<ConcurrencyResult> RunConcurrencyBenchmarkAsync(
        IRagPlugin plugin,
        Func<PluginInput> inputGenerator,
        PluginConfig? config = null,
        int concurrency = 10,
        int operationsPerTask = 5)
    {
        config ??= PluginConfig.Default();

        // Warmup
        await plugin.ExecuteAsync(inputGenerator(), config).ConfigureAwait(false);

        var stopwatch = Stopwatch.StartNew();
        var allDurations = new System.Collections.Concurrent.ConcurrentBag<double>();
        var allSuccesses = new System.Collections.Concurrent.ConcurrentBag<bool>();

        var tasks = Enumerable.Range(0, concurrency).Select(async _ =>
        {
            var taskStopwatch = new Stopwatch();

            for (var i = 0; i < operationsPerTask; i++)
            {
                var input = inputGenerator();

                taskStopwatch.Restart();
                var output = await plugin.ExecuteAsync(input, config).ConfigureAwait(false);
                taskStopwatch.Stop();

                allDurations.Add(taskStopwatch.Elapsed.TotalMilliseconds);
                allSuccesses.Add(output.Success);
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        stopwatch.Stop();

        var durations = allDurations.ToList();

        return new ConcurrencyResult
        {
            PluginId = plugin.Id,
            Concurrency = concurrency,
            OperationsPerTask = operationsPerTask,
            TotalOperations = durations.Count,
            TotalDurationMs = stopwatch.Elapsed.TotalMilliseconds,
            SuccessCount = allSuccesses.Count(s => s),
            FailureCount = allSuccesses.Count(s => !s),
            MinDurationMs = durations.Count > 0 ? durations.Min() : 0,
            MaxDurationMs = durations.Count > 0 ? durations.Max() : 0,
            MeanDurationMs = durations.Count > 0 ? durations.Average() : 0,
            ThroughputPerSecond = durations.Count / (stopwatch.Elapsed.TotalMilliseconds / 1000.0)
        };
    }

    /// <summary>
    /// Runs a latency profile benchmark with different payload sizes.
    /// </summary>
    /// <param name="plugin">The plugin to benchmark.</param>
    /// <param name="payloadSizeGenerator">Function to generate inputs of specific sizes.</param>
    /// <param name="payloadSizes">Payload sizes to test.</param>
    /// <param name="config">Plugin configuration to use.</param>
    /// <param name="iterationsPerSize">Number of iterations per size.</param>
    /// <returns>Latency profile results.</returns>
    public static async Task<LatencyProfileResult> RunLatencyProfileAsync(
        IRagPlugin plugin,
        Func<int, PluginInput> payloadSizeGenerator,
        int[] payloadSizes,
        PluginConfig? config = null,
        int iterationsPerSize = 5)
    {
        config ??= PluginConfig.Default();

        var profiles = new Dictionary<int, PerformanceStatistics>();
        var stopwatch = new Stopwatch();

        foreach (var size in payloadSizes)
        {
            var durations = new List<double>(iterationsPerSize);

            // Warmup for this size
            await plugin.ExecuteAsync(payloadSizeGenerator(size), config).ConfigureAwait(false);

            for (var i = 0; i < iterationsPerSize; i++)
            {
                var input = payloadSizeGenerator(size);

                stopwatch.Restart();
                await plugin.ExecuteAsync(input, config).ConfigureAwait(false);
                stopwatch.Stop();

                durations.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            profiles[size] = new PerformanceStatistics
            {
                Iterations = iterationsPerSize,
                MinMs = durations.Count > 0 ? durations.Min() : 0,
                MaxMs = durations.Count > 0 ? durations.Max() : 0,
                MeanMs = durations.Count > 0 ? durations.Average() : 0,
                MedianMs = CalculateMedian(durations),
                StdDevMs = CalculateStandardDeviation(durations),
                P95Ms = CalculatePercentile(durations, 95),
                P99Ms = CalculatePercentile(durations, 99)
            };
        }

        return new LatencyProfileResult
        {
            PluginId = plugin.Id,
            ProfilesByPayloadSize = profiles
        };
    }

    #region Private Helpers

    private static double CalculateMedian(List<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }

    private static double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count == 0) return 0;
        var mean = values.Average();
        var sumSquaredDiffs = values.Sum(v => Math.Pow(v - mean, 2));
        return Math.Sqrt(sumSquaredDiffs / values.Count);
    }

    private static double CalculatePercentile(List<double> values, int percentile)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }

    #endregion
}

/// <summary>
/// Options for benchmark execution.
/// </summary>
public sealed record BenchmarkOptions
{
    /// <summary>
    /// Number of warmup iterations before measurement.
    /// </summary>
    public int WarmupIterations { get; init; } = 3;

    /// <summary>
    /// Number of measurement iterations.
    /// </summary>
    public int Iterations { get; init; } = 100;

    /// <summary>
    /// Whether to measure memory usage.
    /// </summary>
    public bool MeasureMemory { get; init; } = false;

    /// <summary>
    /// Delay between iterations in milliseconds (0 = no delay).
    /// </summary>
    public int DelayBetweenIterationsMs { get; init; } = 0;

    /// <summary>
    /// Default benchmark options.
    /// </summary>
    public static BenchmarkOptions Default => new();

    /// <summary>
    /// Quick benchmark options (fewer iterations).
    /// </summary>
    public static BenchmarkOptions Quick => new() { Iterations = 10, WarmupIterations = 1 };

    /// <summary>
    /// Thorough benchmark options (more iterations with memory measurement).
    /// </summary>
    public static BenchmarkOptions Thorough => new()
    {
        Iterations = 500,
        WarmupIterations = 10,
        MeasureMemory = true
    };
}

/// <summary>
/// Result of a benchmark run.
/// </summary>
public sealed record BenchmarkResult
{
    /// <summary>
    /// Plugin ID that was benchmarked.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Plugin version that was benchmarked.
    /// </summary>
    public required string PluginVersion { get; init; }

    /// <summary>
    /// Configuration used for the benchmark.
    /// </summary>
    public required PluginConfig Configuration { get; init; }

    /// <summary>
    /// Benchmark options used.
    /// </summary>
    public required BenchmarkOptions Options { get; init; }

    /// <summary>
    /// Start time of the benchmark.
    /// </summary>
    public DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// End time of the benchmark.
    /// </summary>
    public DateTimeOffset EndTime { get; set; }

    /// <summary>
    /// Number of successful executions.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Number of failed executions.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Individual execution durations in milliseconds.
    /// </summary>
    public IReadOnlyList<double> Durations { get; set; } = [];

    /// <summary>
    /// Memory delta in bytes (if measured).
    /// </summary>
    public long? MemoryDelta { get; set; }

    /// <summary>
    /// Success rate (0.0 to 1.0).
    /// </summary>
    public double SuccessRate => Durations.Count > 0 ? (double)SuccessCount / Durations.Count : 0;

    /// <summary>
    /// Minimum execution time in milliseconds.
    /// </summary>
    public double MinMs => Durations.Count > 0 ? Durations.Min() : 0;

    /// <summary>
    /// Maximum execution time in milliseconds.
    /// </summary>
    public double MaxMs => Durations.Count > 0 ? Durations.Max() : 0;

    /// <summary>
    /// Mean execution time in milliseconds.
    /// </summary>
    public double MeanMs => Durations.Count > 0 ? Durations.Average() : 0;

    /// <summary>
    /// Total benchmark duration.
    /// </summary>
    public TimeSpan TotalDuration => EndTime - StartTime;

    /// <summary>
    /// Returns a formatted summary of the benchmark results.
    /// </summary>
    public override string ToString()
    {
        var memory = MemoryDelta.HasValue ? $"\n  Memory Delta: {MemoryDelta.Value / 1024.0:F2} KB" : "";
        return $"""
            Benchmark Results for {PluginId} v{PluginVersion}
              Iterations:   {Durations.Count} ({SuccessCount} success, {FailureCount} failed)
              Success Rate: {SuccessRate * 100:F1}%
              Min:          {MinMs:F2} ms
              Max:          {MaxMs:F2} ms
              Mean:         {MeanMs:F2} ms
              Total Time:   {TotalDuration.TotalSeconds:F2} s{memory}
            """;
    }
}

/// <summary>
/// Result of a throughput benchmark.
/// </summary>
public sealed record ThroughputResult
{
    /// <summary>
    /// Plugin ID that was benchmarked.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Total number of operations performed.
    /// </summary>
    public int TotalOperations { get; init; }

    /// <summary>
    /// Number of successful operations.
    /// </summary>
    public int SuccessfulOperations { get; init; }

    /// <summary>
    /// Total duration of the benchmark in milliseconds.
    /// </summary>
    public double TotalDurationMs { get; init; }

    /// <summary>
    /// Operations per second.
    /// </summary>
    public double OperationsPerSecond { get; init; }

    /// <summary>
    /// Success rate (0.0 to 1.0).
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Returns a formatted summary of the throughput results.
    /// </summary>
    public override string ToString()
    {
        return $"""
            Throughput Results for {PluginId}
              Total Operations: {TotalOperations}
              Duration:         {TotalDurationMs / 1000:F2} s
              Throughput:       {OperationsPerSecond:F2} ops/s
              Success Rate:     {SuccessRate * 100:F1}%
            """;
    }
}

/// <summary>
/// Result of a concurrency benchmark.
/// </summary>
public sealed record ConcurrencyResult
{
    /// <summary>
    /// Plugin ID that was benchmarked.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Number of concurrent tasks.
    /// </summary>
    public int Concurrency { get; init; }

    /// <summary>
    /// Operations per task.
    /// </summary>
    public int OperationsPerTask { get; init; }

    /// <summary>
    /// Total number of operations.
    /// </summary>
    public int TotalOperations { get; init; }

    /// <summary>
    /// Total duration in milliseconds.
    /// </summary>
    public double TotalDurationMs { get; init; }

    /// <summary>
    /// Number of successful operations.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Number of failed operations.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Minimum operation duration in milliseconds.
    /// </summary>
    public double MinDurationMs { get; init; }

    /// <summary>
    /// Maximum operation duration in milliseconds.
    /// </summary>
    public double MaxDurationMs { get; init; }

    /// <summary>
    /// Mean operation duration in milliseconds.
    /// </summary>
    public double MeanDurationMs { get; init; }

    /// <summary>
    /// Throughput in operations per second.
    /// </summary>
    public double ThroughputPerSecond { get; init; }

    /// <summary>
    /// Returns a formatted summary of the concurrency results.
    /// </summary>
    public override string ToString()
    {
        return $"""
            Concurrency Results for {PluginId}
              Concurrency:     {Concurrency} tasks x {OperationsPerTask} ops
              Total Operations: {TotalOperations}
              Duration:        {TotalDurationMs / 1000:F2} s
              Throughput:      {ThroughputPerSecond:F2} ops/s
              Success/Failure: {SuccessCount}/{FailureCount}
              Latency:         {MinDurationMs:F2}ms min / {MeanDurationMs:F2}ms mean / {MaxDurationMs:F2}ms max
            """;
    }
}

/// <summary>
/// Result of a latency profile benchmark.
/// </summary>
public sealed record LatencyProfileResult
{
    /// <summary>
    /// Plugin ID that was benchmarked.
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Performance statistics by payload size.
    /// </summary>
    public IReadOnlyDictionary<int, PerformanceStatistics> ProfilesByPayloadSize { get; init; } =
        new Dictionary<int, PerformanceStatistics>();

    /// <summary>
    /// Returns a formatted summary of the latency profile.
    /// </summary>
    public override string ToString()
    {
        var lines = new List<string> { $"Latency Profile for {PluginId}" };

        foreach (var (size, stats) in ProfilesByPayloadSize.OrderBy(kv => kv.Key))
        {
            lines.Add($"  Payload Size {size}: mean={stats.MeanMs:F2}ms, p95={stats.P95Ms:F2}ms, p99={stats.P99Ms:F2}ms");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
