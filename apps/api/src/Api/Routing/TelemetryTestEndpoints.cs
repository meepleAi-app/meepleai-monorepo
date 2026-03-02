using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.Routing;

/// <summary>
/// Telemetry test endpoints for HyperDX integration testing.
/// These endpoints generate logs and traces for validation purposes.
/// </summary>
/// <remarks>
/// Issue #1565: Integration Testing - Backend Telemetry
/// Epic: #1561 (HyperDX Implementation)
///
/// Purpose: Generate test logs/traces to verify HyperDX ingestion:
/// - Log structure validation (timestamp, level, message, service.name)
/// - Trace correlation (log → trace linking)
/// - Sensitive data redaction
/// - Performance testing (100+ events in 1 minute)
/// </remarks>
internal static class TelemetryTestEndpoints
{
    private static readonly ActivitySource ActivitySource = new("MeepleAI.TelemetryTests", "1.0.0");

    private static readonly string[] TestErrorVerifyFields = { "timestamp", "level", "message", "service.name", "correlation_id" };
    private static readonly string[] TestTraceVerifySpans = { "TelemetryTest.GenerateTrace", "TelemetryTest.ChildOperation" };
    private static readonly string[] SensitiveDataVerify =
    {
        "password field should show [REDACTED]",
        "apiKey field should show [REDACTED]",
        "token field should show [REDACTED]",
        "username field should be visible"
    };

    /// <summary>
    /// Maps telemetry test endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapTelemetryTestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/test")
            .WithTags("Telemetry Testing")
            .WithOpenApi();

        // Test Error Logging
        group.MapPost("/error", GenerateTestError)
            .WithName("GenerateTestError")
            .WithSummary("Generate a test error log for HyperDX ingestion testing")
            .WithDescription("Creates structured error log with correlation ID for trace linking")
            .ExcludeFromDescription(); // Hide from production API docs

        // Test Trace Generation
        group.MapGet("/trace", GenerateTestTrace)
            .WithName("GenerateTestTrace")
            .WithSummary("Generate a test distributed trace")
            .WithDescription("Creates multi-span trace with parent-child relationships")
            .ExcludeFromDescription();

        // Test Sensitive Data Redaction
        group.MapPost("/sensitive", TestSensitiveDataRedaction)
            .WithName("TestSensitiveDataRedaction")
            .WithSummary("Test sensitive data redaction in logs/traces")
            .WithDescription("Verifies password/token/apiKey fields are redacted")
            .ExcludeFromDescription();

        // Bulk Log/Trace Generation (Performance Testing)
        group.MapPost("/bulk", GenerateBulkTelemetry)
            .WithName("GenerateBulkTelemetry")
            .WithSummary("Generate bulk logs/traces for performance testing")
            .WithDescription("Creates N logs and traces for ingestion stress testing")
            .ExcludeFromDescription();

        return endpoints;
    }

    /// <summary>
    /// POST /api/v1/test/error
    /// Generates a structured error log with correlation ID.
    /// </summary>
    private static IResult GenerateTestError(
        ILogger<Program> logger,
        IWebHostEnvironment env,
        HttpContext context)
    {
        if (env.IsProduction())
        {
            logger.LogError("Telemetry test endpoints are not available in Production environment");
            return Results.StatusCode(403);
        }

        var correlationId = Activity.Current?.Id ?? context.TraceIdentifier;

        logger.LogError(
            "Test error generated for HyperDX ingestion validation. " +
            "CorrelationId: {CorrelationId}, Timestamp: {Timestamp}, Service: {Service}",
            correlationId,
            DateTimeOffset.UtcNow,
            "meepleai-api");

        return Results.Ok(new
        {
            message = "Test error log generated",
            correlationId,
            timestamp = DateTimeOffset.UtcNow,
            serviceName = "meepleai-api",
            logLevel = "error",
            expectedInHyperDX = new
            {
                search = $"service.name:meepleai-api AND level:error AND correlation_id:{correlationId}",
                verifyFields = TestErrorVerifyFields
            }
        });
    }

    /// <summary>
    /// GET /api/v1/test/trace
    /// Generates a distributed trace with multiple spans.
    /// </summary>
    private static async Task<IResult> GenerateTestTrace(
        ILogger<Program> logger,
        IWebHostEnvironment env
        )
    {
        if (env.IsProduction())
        {
            logger.LogError("Telemetry test endpoints are not available in Production environment");
            return Results.StatusCode(403);
        }

        using var activity = ActivitySource.StartActivity("TelemetryTest.GenerateTrace", ActivityKind.Server);

        if (activity == null)
        {
            logger.LogWarning("ActivitySource not properly initialized - OpenTelemetry may be disabled");
            return Results.Problem(
                title: "Tracing Not Available",
                detail: "OpenTelemetry ActivitySource is not initialized. Ensure tracing is configured.",
                statusCode: 500);
        }

        activity.SetTag("test.type", "integration");
        activity.SetTag("service.name", "meepleai-api");

        var traceId = activity.TraceId.ToString();
        var spanId = activity.SpanId.ToString();

        logger.LogInformation(
            "Test trace started. TraceId: {TraceId}, SpanId: {SpanId}",
            traceId,
            spanId);

        // Simulate child span
        using (var childActivity = ActivitySource.StartActivity("TelemetryTest.ChildOperation", ActivityKind.Internal))
        {
            if (childActivity != null)
            {
                childActivity.SetTag("operation", "data-fetch");
                await Task.Delay(10).ConfigureAwait(false); // Simulate work

                logger.LogInformation(
                    "Child span completed. ParentTraceId: {TraceId}, ChildSpanId: {SpanId}",
                    traceId,
                    childActivity.SpanId.ToString());
            }
        }

        logger.LogInformation(
            "Test trace completed. TraceId: {TraceId}",
            traceId);

        return Results.Ok(new
        {
            message = "Test trace generated",
            traceId,
            spanId,
            expectedInHyperDX = new
            {
                search = $"trace_id:{traceId}",
                verifySpans = TestTraceVerifySpans,
                verifyCorrelation = "Click log → should auto-open trace view"
            }
        });
    }

    /// <summary>
    /// POST /api/v1/test/sensitive
    /// Tests sensitive data redaction in logs and traces.
    /// </summary>
    private static IResult TestSensitiveDataRedaction(
        ILogger<Program> logger,
        IWebHostEnvironment env
        )
    {
        if (env.IsProduction())
        {
            logger.LogError("Telemetry test endpoints are not available in Production environment");
            return Results.StatusCode(403);
        }

        using var activity = ActivitySource.StartActivity("TelemetryTest.SensitiveData", ActivityKind.Server);

        if (activity == null)
        {
            logger.LogWarning("ActivitySource not properly initialized - OpenTelemetry may be disabled");
            return Results.Problem(
                title: "Tracing Not Available",
                detail: "OpenTelemetry ActivitySource is not initialized. Ensure tracing is configured.",
                statusCode: 500);
        }

        // SEC-01: Use clearly non-realistic placeholder values for redaction testing
        var testData = new
        {
            username = "test-user",
            password = "PLACEHOLDER_FOR_REDACTION_TEST",
            apiKey = "test_key_not_real_000000",
            token = "Bearer test_token_not_real_000000",
            secret = "test_secret_not_real"
        };

        activity.SetTag("test.username", testData.username);
        activity.SetTag("test.password", testData.password);  // Should be redacted
        activity.SetTag("test.apiKey", testData.apiKey);      // Should be redacted
        activity.SetTag("test.token", testData.token);        // Should be redacted

        logger.LogWarning(
            "Testing sensitive data redaction: User={Username}, Password={Password}, ApiKey={ApiKey}, Token={Token}",
            testData.username,
            testData.password,  // Should be redacted
            testData.apiKey,    // Should be redacted
            testData.token);    // Should be redacted

        return Results.Ok(new
        {
            message = "Sensitive data redaction test completed",
            traceId = activity.TraceId.ToString(),
            expectedInHyperDX = new
            {
                search = $"trace_id:{activity.TraceId}",
                verify = SensitiveDataVerify
            }
        });
    }

    /// <summary>
    /// POST /api/v1/test/bulk?count=100
    /// Generates bulk telemetry for performance testing.
    /// </summary>
    private static async Task<IResult> GenerateBulkTelemetry(
        ILogger<Program> logger,
        IWebHostEnvironment env,
                int count = 100)
    {
        if (env.IsProduction())
        {
            logger.LogError("Telemetry test endpoints are not available in Production environment");
            return Results.StatusCode(403);
        }

        if (count > 1000)
        {
            return Results.BadRequest(new { error = "Maximum count is 1000" });
        }

        var startTime = DateTimeOffset.UtcNow;
        var logs = new List<string>();
        var traces = new List<string>();

        for (int i = 0; i < count; i++)
        {
            using var activity = ActivitySource.StartActivity($"BulkTest.Operation{i}", ActivityKind.Server);
            var traceId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString();

            activity?.SetTag("iteration", i);
            activity?.SetTag("test.type", "bulk");

            logger.LogInformation(
                "Bulk telemetry #{Iteration}: TraceId={TraceId}, Timestamp={Timestamp}",
                i,
                traceId,
                DateTimeOffset.UtcNow);

            logs.Add(traceId);
            traces.Add(traceId);

            // Throttle to avoid overwhelming the system
            if (i % 10 == 0)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        var duration = DateTimeOffset.UtcNow - startTime;

        logger.LogInformation(
            "Bulk telemetry generation completed: Count={Count}, Duration={Duration}ms",
            count,
            duration.TotalMilliseconds);

        return Results.Ok(new
        {
            message = "Bulk telemetry generated",
            count,
            durationMs = duration.TotalMilliseconds,
            logsGenerated = logs.Count,
            tracesGenerated = traces.Count,
            expectedInHyperDX = new
            {
                search = "test.type:bulk",
                verify = new[]
                {
                    $"All {count} logs should appear in HyperDX within 10s",
                    $"All {count} traces should appear in HyperDX",
                    "Search performance should be <1s for queries",
                    "HyperDX resource usage should be <4GB RAM"
                }
            }
        });
    }
}
