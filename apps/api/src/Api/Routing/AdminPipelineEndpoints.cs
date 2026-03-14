using System.Globalization;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoint for aggregated RAG pipeline health.
/// Issue #4879: Pipeline Overview with Live Polling.
/// </summary>
internal static class AdminPipelineEndpoints
{
    /// <summary>P95 threshold in seconds above which a processing stage is considered degraded.</summary>
    private const double DegradedP95ThresholdSeconds = 30.0;

    /// <summary>P95 threshold in seconds above which a processing stage is considered unhealthy.</summary>
    private const double UnhealthyP95ThresholdSeconds = 120.0;

    public static RouteGroupBuilder MapAdminPipelineEndpoints(this RouteGroupBuilder group)
    {
        var pipelineGroup = group.MapGroup("/admin/kb/pipeline")
            .WithTags("Admin", "Pipeline")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/kb/pipeline/health
        pipelineGroup.MapGet("/health", GetPipelineHealth)
            .WithName("GetPipelineHealth")
            .WithSummary("Get aggregated RAG pipeline health across all stages");

        return group;
    }

    private static async Task<IResult> GetPipelineHealth(
        IInfrastructureHealthService healthService,
        IProcessingMetricsService metricsService,
        IHttpClientFactory httpClientFactory,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Run independent queries in parallel
        var healthTask = healthService.GetAllServicesHealthAsync(ct);
        var metricsTask = metricsService.GetAllStepStatisticsAsync(ct);
        var storageTask = mediator.Send(new GetPdfStorageHealthQuery(), ct);
        var queueTask = mediator.Send(new GetProcessingQueueQuery(
            StatusFilter: null,
            SearchText: null,
            FromDate: null,
            ToDate: null,
            Page: 1,
            PageSize: 10), ct);

        // Embedding service metrics (fire and forget on failure)
        var embeddingMetricsTask = GetEmbeddingMetricsSafe(httpClientFactory, logger, ct);

        await Task.WhenAll(healthTask, metricsTask, storageTask, queueTask, embeddingMetricsTask).ConfigureAwait(false);

        var allHealth = await healthTask.ConfigureAwait(false);
        var stepMetrics = await metricsTask.ConfigureAwait(false);
        var storageHealth = await storageTask.ConfigureAwait(false);
        var queueResult = await queueTask.ConfigureAwait(false);
        var embeddingMetrics = await embeddingMetricsTask.ConfigureAwait(false);

        // Map service health by name for easy lookup
        var healthByName = allHealth.ToDictionary(
            h => h.ServiceName,
            h => h,
            StringComparer.OrdinalIgnoreCase);

        // Count active/failed jobs
        var activeJobs = queueResult.Jobs.Count(j =>
            string.Equals(j.Status, "Processing", StringComparison.OrdinalIgnoreCase));
        var queuedJobs = queueResult.Jobs.Count(j =>
            string.Equals(j.Status, "Queued", StringComparison.OrdinalIgnoreCase));
        var failedJobs = queueResult.Jobs.Count(j =>
            string.Equals(j.Status, "Failed", StringComparison.OrdinalIgnoreCase));

        // Build pipeline stages
        var stages = new List<object>
        {
            BuildIngestStage(activeJobs, queuedJobs, failedJobs),
            BuildProcessingStage("Extract", "Extracting", stepMetrics),
            BuildProcessingStage("Chunk", "Chunking", stepMetrics),
            BuildEmbedStage(healthByName, embeddingMetrics),
            BuildIndexStage(storageHealth),
            BuildRetrieveStage(healthByName),
            BuildGenerateStage(healthByName),
        };

        var healthyCount = stages.Count(s => string.Equals(
            ((dynamic)s).status, "healthy", StringComparison.Ordinal));
        var warningCount = stages.Count(s => string.Equals(
            ((dynamic)s).status, "warning", StringComparison.Ordinal));
        var errorCount = stages.Count(s => string.Equals(
            ((dynamic)s).status, "error", StringComparison.Ordinal));

        // Recent activity: last 10 completed/failed jobs
        var recentActivity = queueResult.Jobs
            .Where(j => string.Equals(j.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(j.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
            .Take(10)
            .Select(j => new
            {
                jobId = j.Id,
                fileName = j.PdfFileName,
                status = j.Status,
                completedAt = j.CompletedAt,
                durationMs = j.StartedAt.HasValue && j.CompletedAt.HasValue
                    ? (long)(j.CompletedAt.Value - j.StartedAt.Value).TotalMilliseconds
                    : (long?)null,
            })
            .ToList();

        // Distribution from storage health
        var distribution = new
        {
            totalDocuments = storageHealth.Postgres.TotalDocuments,
            totalChunks = storageHealth.Postgres.TotalChunks,
            vectorCount = storageHealth.Qdrant.VectorCount,
            totalFiles = storageHealth.FileStorage.TotalFiles,
            storageSizeFormatted = storageHealth.FileStorage.TotalSizeFormatted,
        };

        return Results.Ok(new
        {
            stages,
            summary = new { healthyCount, warningCount, errorCount },
            recentActivity,
            distribution,
            checkedAt = DateTime.UtcNow,
        });
    }

    private static object BuildIngestStage(int activeJobs, int queuedJobs, int failedJobs)
    {
        var status = failedJobs > 0 ? "warning" : "healthy";
        return new
        {
            name = "Ingest",
            status,
            metrics = new
            {
                activeJobs,
                queuedJobs,
                failedJobs,
            },
        };
    }

    private static object BuildProcessingStage(
        string stageName,
        string stepKey,
        Dictionary<string, StepDurationStats> stepMetrics)
    {
        // Try to find the step metrics (case-insensitive match on step name)
        var stats = stepMetrics.FirstOrDefault(kv =>
            kv.Key.Contains(stepKey, StringComparison.OrdinalIgnoreCase)).Value;

        string status;
        if (stats is null)
        {
            status = "healthy"; // No data yet = assume healthy
        }
        else if (stats.P95DurationSeconds > UnhealthyP95ThresholdSeconds)
        {
            status = "error";
        }
        else if (stats.P95DurationSeconds > DegradedP95ThresholdSeconds)
        {
            status = "warning";
        }
        else
        {
            status = "healthy";
        }

        return new
        {
            name = stageName,
            status,
            metrics = new
            {
                avgDurationMs = stats is not null
                    ? Math.Round(stats.AverageDurationSeconds * 1000, 0).ToString(CultureInfo.InvariantCulture)
                    : (string?)null,
                p95Ms = stats is not null
                    ? Math.Round(stats.P95DurationSeconds * 1000, 0).ToString(CultureInfo.InvariantCulture)
                    : (string?)null,
                sampleSize = stats?.SampleSize ?? 0,
            },
        };
    }

    private static object BuildEmbedStage(
        Dictionary<string, ServiceHealthStatus> healthByName,
        EmbeddingMetricsSnapshot? embeddingMetrics)
    {
        var embeddingHealth = healthByName.GetValueOrDefault("embedding");
        string status;
        if (embeddingHealth is null || embeddingHealth.State == HealthState.Unhealthy)
        {
            status = "error";
        }
        else if (embeddingHealth.State == HealthState.Degraded)
        {
            status = "warning";
        }
        else
        {
            status = "healthy";
        }

        return new
        {
            name = "Embed",
            status,
            metrics = new
            {
                serviceHealth = embeddingHealth?.State.ToString() ?? "unknown",
                requestsTotal = embeddingMetrics?.RequestsTotal ?? 0,
                avgDurationMs = embeddingMetrics?.AvgDurationMs ?? 0,
                failureRate = embeddingMetrics?.FailureRate ?? 0,
            },
        };
    }

    private static object BuildIndexStage(PdfStorageHealthDto storageHealth)
    {
        var status = storageHealth.Qdrant.IsAvailable ? "healthy" : "error";
        return new
        {
            name = "Index",
            status,
            metrics = new
            {
                vectorCount = storageHealth.Qdrant.VectorCount,
                memoryFormatted = storageHealth.Qdrant.MemoryFormatted,
                isAvailable = storageHealth.Qdrant.IsAvailable,
            },
        };
    }

    private static object BuildRetrieveStage(Dictionary<string, ServiceHealthStatus> healthByName)
    {
        var qdrantHealth = healthByName.GetValueOrDefault("qdrant");
        string status;
        if (qdrantHealth is null || qdrantHealth.State == HealthState.Unhealthy)
        {
            status = "error";
        }
        else if (qdrantHealth.State == HealthState.Degraded)
        {
            status = "warning";
        }
        else
        {
            status = "healthy";
        }

        return new
        {
            name = "Retrieve",
            status,
            metrics = new
            {
                qdrantHealth = qdrantHealth?.State.ToString() ?? "unknown",
            },
        };
    }

    private static object BuildGenerateStage(Dictionary<string, ServiceHealthStatus> healthByName)
    {
        // No LLM health check in monitored services - default to healthy
        // Future: add openrouter health check
        return new
        {
            name = "Generate",
            status = "healthy",
            metrics = new
            {
                note = "LLM service via OpenRouter",
            },
        };
    }

    /// <summary>
    /// Fetches embedding service Prometheus metrics without failing the whole endpoint.
    /// </summary>
    private static async Task<EmbeddingMetricsSnapshot?> GetEmbeddingMetricsSafe(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("EmbeddingService");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var response = await client.GetAsync("/metrics", cts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var text = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            return ParsePrometheusMetrics(text);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch embedding metrics for pipeline health");
            return null;
        }
    }

    private static EmbeddingMetricsSnapshot ParsePrometheusMetrics(string text)
    {
        long requestsTotal = 0;
        long failuresTotal = 0;
        double durationMsSum = 0;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var parts = trimmed.Split(' ', 2);
            if (parts.Length < 2)
            {
                continue;
            }

            var key = parts[0];
            var value = parts[1];

            if (string.Equals(key, "embed_requests_total", StringComparison.Ordinal)
                && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var req))
            {
                requestsTotal = req;
            }
            else if (string.Equals(key, "embed_failures_total", StringComparison.Ordinal)
                     && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fail))
            {
                failuresTotal = fail;
            }
            else if (string.Equals(key, "embed_duration_ms_sum", StringComparison.Ordinal)
                     && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
            {
                durationMsSum = dur;
            }
        }

        var avgDuration = requestsTotal > 0 ? Math.Round(durationMsSum / requestsTotal, 2) : 0.0;
        var failureRate = requestsTotal > 0 ? Math.Round((double)failuresTotal / requestsTotal * 100.0, 2) : 0.0;

        return new EmbeddingMetricsSnapshot(requestsTotal, avgDuration, failureRate);
    }

    private sealed record EmbeddingMetricsSnapshot(long RequestsTotal, double AvgDurationMs, double FailureRate);
}
