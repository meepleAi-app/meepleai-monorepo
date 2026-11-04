using System.Globalization;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
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

    // internal (not private) so AdminPipelineHealthResponseShapeTests can invoke it with a stub IMediator.
    internal static async Task<IResult> GetPipelineHealth(
        IMediator mediator,
        CancellationToken ct)
    {
        // #4512: one send. The four queries and the embedding metrics are read by
        // GetPipelineHealthSnapshotQueryHandler, which also keeps the two rules this endpoint used
        // to keep here: the four queries go one at a time (#4059, they share the request's
        // DbContext), and the metrics read runs alongside them, outside the MediatR pipeline.
        var snapshot = await mediator.Send(new GetPipelineHealthSnapshotQuery(), ct).ConfigureAwait(false);

        var allHealth = snapshot.ServiceHealth;
        var stepMetrics = snapshot.StepDurations;
        var storageHealth = snapshot.StorageHealth;
        var queueResult = snapshot.Queue;
        var embeddingMetrics = snapshot.EmbeddingMetrics;

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
            vectorCount = storageHealth.VectorStore.VectorCount,
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
        EmbeddingServiceMetricsDto embeddingMetrics)
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
                requestsTotal = embeddingMetrics.RequestsTotal,
                avgDurationMs = embeddingMetrics.AvgDurationMs,
                failureRate = embeddingMetrics.FailureRate,
            },
        };
    }

    private static object BuildIndexStage(PdfStorageHealthDto storageHealth)
    {
        var status = storageHealth.VectorStore.IsAvailable ? "healthy" : "error";
        return new
        {
            name = "Index",
            status,
            metrics = new
            {
                vectorCount = storageHealth.VectorStore.VectorCount,
                isAvailable = storageHealth.VectorStore.IsAvailable,
            },
        };
    }

    private static object BuildRetrieveStage(Dictionary<string, ServiceHealthStatus> healthByName)
    {
        var postgresHealth = healthByName.GetValueOrDefault("postgres");
        string status;
        if (postgresHealth is null || postgresHealth.State == HealthState.Unhealthy)
        {
            status = "error";
        }
        else if (postgresHealth.State == HealthState.Degraded)
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
                vectorStoreHealth = postgresHealth?.State.ToString() ?? "unknown",
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
}
