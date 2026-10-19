using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Routing;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// #4512 moved the four sends and the embedding metrics read of <c>/admin/kb/pipeline/health</c>
/// from the endpoint into <see cref="GetPipelineHealthSnapshotQueryHandler"/>; the endpoint now
/// builds the answer from the snapshot. The web reads that answer as <c>PipelineHealthResponse</c>
/// (<c>apps/web/src/lib/api/clients/admin/adminAiClient.ts</c>), so it must not change: the same
/// inputs — four query results and one Prometheus body, answered with 200 and with 503 — must give
/// the JSON <c>main-dev</c> gave.
/// The inputs reach every branch that shapes the answer: a degraded and a slow stage, a job of each
/// status, a job without timestamps, and an average that rounds (1001 / 8 = 125.125 → 125.12).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "4512")]
public sealed class AdminPipelineHealthResponseShapeTests
{
    private const string MetricsBody =
        "# HELP embed_requests_total Total embedding requests\n" +
        "# TYPE embed_requests_total counter\n" +
        "embed_requests_total 8\n" +
        "# HELP embed_failures_total Total embedding failures\n" +
        "# TYPE embed_failures_total counter\n" +
        "embed_failures_total 1\n" +
        "# HELP embed_duration_ms_sum Cumulative embedding latency in ms\n" +
        "# TYPE embed_duration_ms_sum counter\n" +
        "embed_duration_ms_sum 1001.0\n" +
        "# HELP embed_total_chars_sum Cumulative characters processed\n" +
        "# TYPE embed_total_chars_sum counter\n" +
        "embed_total_chars_sum 52344.0\n";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    /// <summary>
    /// What <c>GetPipelineHealth</c> answered on <c>main-dev</c> at 3286ca293, before #4512, for the
    /// inputs below — captured by running the old endpoint, not written by hand. Only
    /// <c>checkedAt</c> is masked: it is the clock.
    /// </summary>
    private const string GoldenWithMetrics = """
        {
          "stages": [
            {
              "name": "Ingest",
              "status": "warning",
              "metrics": {
                "activeJobs": 1,
                "queuedJobs": 1,
                "failedJobs": 1
              }
            },
            {
              "name": "Extract",
              "status": "warning",
              "metrics": {
                "avgDurationMs": "12500",
                "p95Ms": "45000",
                "sampleSize": 10
              }
            },
            {
              "name": "Chunk",
              "status": "healthy",
              "metrics": {
                "avgDurationMs": "1250",
                "p95Ms": "2500",
                "sampleSize": 7
              }
            },
            {
              "name": "Embed",
              "status": "warning",
              "metrics": {
                "serviceHealth": "Degraded",
                "requestsTotal": 8,
                "avgDurationMs": 125.12,
                "failureRate": 12.5
              }
            },
            {
              "name": "Index",
              "status": "healthy",
              "metrics": {
                "vectorCount": 42,
                "isAvailable": true
              }
            },
            {
              "name": "Retrieve",
              "status": "healthy",
              "metrics": {
                "vectorStoreHealth": "Healthy"
              }
            },
            {
              "name": "Generate",
              "status": "healthy",
              "metrics": {
                "note": "LLM service via OpenRouter"
              }
            }
          ],
          "summary": {
            "healthyCount": 4,
            "warningCount": 3,
            "errorCount": 0
          },
          "recentActivity": [
            {
              "jobId": "00000000-0000-0000-0000-000000000003",
              "fileName": "rules-3.pdf",
              "status": "Failed",
              "completedAt": "2026-10-19T11:57:30\u002B00:00",
              "durationMs": 30000
            },
            {
              "jobId": "00000000-0000-0000-0000-000000000004",
              "fileName": "rules-4.pdf",
              "status": "Completed",
              "completedAt": "2026-10-19T11:57:30\u002B00:00",
              "durationMs": 90000
            },
            {
              "jobId": "00000000-0000-0000-0000-000000000005",
              "fileName": "rules-5.pdf",
              "status": "Completed",
              "completedAt": null,
              "durationMs": null
            }
          ],
          "distribution": {
            "totalDocuments": 3,
            "totalChunks": 42,
            "vectorCount": 42,
            "totalFiles": 3,
            "storageSizeFormatted": "1 KB"
          },
          "checkedAt": "\u003CcheckedAt\u003E"
        }
        """;

    /// <summary>
    /// The same, captured the same way, when <c>/metrics</c> answers 503: the Embed stage reports
    /// zeros. Only those three numbers differ from <see cref="GoldenWithMetrics"/>.
    /// </summary>
    private const string GoldenServiceDown = """
        {
          "stages": [
            {
              "name": "Ingest",
              "status": "warning",
              "metrics": {
                "activeJobs": 1,
                "queuedJobs": 1,
                "failedJobs": 1
              }
            },
            {
              "name": "Extract",
              "status": "warning",
              "metrics": {
                "avgDurationMs": "12500",
                "p95Ms": "45000",
                "sampleSize": 10
              }
            },
            {
              "name": "Chunk",
              "status": "healthy",
              "metrics": {
                "avgDurationMs": "1250",
                "p95Ms": "2500",
                "sampleSize": 7
              }
            },
            {
              "name": "Embed",
              "status": "warning",
              "metrics": {
                "serviceHealth": "Degraded",
                "requestsTotal": 0,
                "avgDurationMs": 0,
                "failureRate": 0
              }
            },
            {
              "name": "Index",
              "status": "healthy",
              "metrics": {
                "vectorCount": 42,
                "isAvailable": true
              }
            },
            {
              "name": "Retrieve",
              "status": "healthy",
              "metrics": {
                "vectorStoreHealth": "Healthy"
              }
            },
            {
              "name": "Generate",
              "status": "healthy",
              "metrics": {
                "note": "LLM service via OpenRouter"
              }
            }
          ],
          "summary": {
            "healthyCount": 4,
            "warningCount": 3,
            "errorCount": 0
          },
          "recentActivity": [
            {
              "jobId": "00000000-0000-0000-0000-000000000003",
              "fileName": "rules-3.pdf",
              "status": "Failed",
              "completedAt": "2026-10-19T11:57:30\u002B00:00",
              "durationMs": 30000
            },
            {
              "jobId": "00000000-0000-0000-0000-000000000004",
              "fileName": "rules-4.pdf",
              "status": "Completed",
              "completedAt": "2026-10-19T11:57:30\u002B00:00",
              "durationMs": 90000
            },
            {
              "jobId": "00000000-0000-0000-0000-000000000005",
              "fileName": "rules-5.pdf",
              "status": "Completed",
              "completedAt": null,
              "durationMs": null
            }
          ],
          "distribution": {
            "totalDocuments": 3,
            "totalChunks": 42,
            "vectorCount": 42,
            "totalFiles": 3,
            "storageSizeFormatted": "1 KB"
          },
          "checkedAt": "\u003CcheckedAt\u003E"
        }
        """;

    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public Task ResponseShape_IsTheOneMainDevAnswered() =>
        AssertAnswersAsync(HttpStatusCode.OK, GoldenWithMetrics);

    /// <summary>
    /// #4512 review: the web reads this answer only through a TypeScript type, with no schema, so a
    /// change in what the service-down case answers would reach the page unnoticed.
    /// </summary>
    [Fact]
    public Task ResponseShape_WhenTheEmbeddingServiceAnswersAnError_IsTheOneMainDevAnswered() =>
        AssertAnswersAsync(HttpStatusCode.ServiceUnavailable, GoldenServiceDown);

    private static async Task AssertAnswersAsync(HttpStatusCode metricsStatus, string golden)
    {
        var result = await AdminPipelineEndpoints.GetPipelineHealth(
            new CannedMediator(metricsStatus),
            TestContext.Current.CancellationToken);

        // Line by line: FluentAssertions cannot render the mismatch of two long JSON strings.
        Normalize(result).Split('\n').Should().Equal(
            JsonNode.Parse(golden)!.ToJsonString(Indented).Split('\n'),
            "the endpoint now sends one query instead of four, and the JSON it answers must not change");
    }

    private static string Normalize(IResult result)
    {
        var value = ((IValueHttpResult)result).Value;
        var node = JsonNode.Parse(JsonSerializer.Serialize(value, ApiJson))!;
        node["checkedAt"] = "<checkedAt>";
        return node.ToJsonString(Indented);
    }

    private static readonly DateTime At = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset AtOffset = new(At);

    private static ProcessingJobDto Job(string status, int minutesAgo, int? durationSeconds) => new(
        Id: Guid.Parse($"00000000-0000-0000-0000-{minutesAgo:D12}"),
        PdfDocumentId: Guid.Parse($"10000000-0000-0000-0000-{minutesAgo:D12}"),
        PdfFileName: $"rules-{minutesAgo}.pdf",
        UserId: Guid.Parse("20000000-0000-0000-0000-000000000000"),
        Status: status,
        Priority: 0,
        CurrentStep: null,
        CreatedAt: AtOffset.AddMinutes(-minutesAgo),
        StartedAt: durationSeconds is null ? null : AtOffset.AddMinutes(-minutesAgo),
        CompletedAt: durationSeconds is null ? null : AtOffset.AddMinutes(-minutesAgo).AddSeconds(durationSeconds.Value),
        ErrorMessage: status == "Failed" ? "boom" : null,
        RetryCount: 0,
        MaxRetries: 3,
        CanRetry: status == "Failed");

    /// <summary>
    /// Answers the endpoint's one query by running the real handler — with this same mediator for
    /// its four sends and the real reader over a Prometheus body — and the four sends with the
    /// canned results.
    /// </summary>
    private sealed class CannedMediator(HttpStatusCode metricsStatus = HttpStatusCode.OK) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is GetPipelineHealthSnapshotQuery snapshotQuery)
            {
                var reader = new EmbeddingServiceMetricsReader(
                    new PrometheusFactory(metricsStatus), TimeProvider.System, NullLogger<EmbeddingServiceMetricsReader>.Instance);
                object snapshot = await new GetPipelineHealthSnapshotQueryHandler(this, reader)
                    .Handle(snapshotQuery, cancellationToken)
                    .ConfigureAwait(false);
                return (TResponse)snapshot;
            }

            return (TResponse)Respond(request);
        }

        private static object Respond(object request) => request switch
        {
            GetServiceHealthStatusesQuery => (IReadOnlyCollection<ServiceHealthStatus>)new[]
            {
                new ServiceHealthStatus("postgres", HealthState.Healthy, null, At, TimeSpan.Zero),
                new ServiceHealthStatus("embedding", HealthState.Degraded, "slow", At, TimeSpan.Zero),
            },
            GetStepDurationStatsQuery => new Dictionary<string, StepDurationStats>(StringComparer.Ordinal)
            {
                ["Extracting"] = new StepDurationStats("Extracting", 12.5, 10.0, 45.0, 60.0, 10),
                ["Chunking"] = new StepDurationStats("Chunking", 1.25, 1.0, 2.5, 3.0, 7),
            },
            GetPdfStorageHealthQuery => new PdfStorageHealthDto(
                new PostgresInfoDto(3, 42, 1.5),
                new VectorStoreInfoDto(42, true),
                new FileStorageInfoDto(3, 1024, "1 KB", new Dictionary<string, int>(StringComparer.Ordinal) { ["Ready"] = 3 }),
                "healthy",
                At),
            GetProcessingQueueQuery => new PaginatedQueueResponse(
                new[]
                {
                    Job("Processing", 1, null),
                    Job("Queued", 2, null),
                    Job("Failed", 3, 30),
                    Job("Completed", 4, 90),
                    Job("Completed", 5, null),
                },
                5, 1, 10, 1),
            _ => throw new NotSupportedException($"Unexpected query {request.GetType().Name}"),
        };

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => throw new NotSupportedException();
    }

    /// <summary>
    /// The embedding service's <c>/metrics</c>: the Prometheus body, with the given status. With an
    /// error status the body is the same, so only the status can be what turns the metrics to zeros.
    /// </summary>
    private sealed class PrometheusFactory(HttpStatusCode status) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new Answer(status)) { BaseAddress = new Uri("http://embedding-service:8000") };

        private sealed class Answer(HttpStatusCode status) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(status)
                {
                    Content = new StringContent(MetricsBody),
                });
        }
    }
}
