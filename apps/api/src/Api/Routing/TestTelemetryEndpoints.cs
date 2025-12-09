using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Api.Routing;

/// <summary>
/// Test endpoints for OpenTelemetry telemetry verification (Issue #1567)
/// </summary>
public static class TestTelemetryEndpoints
{
    private static readonly ActivitySource TestActivitySource = new("test-telemetry", "1.0.0");

    public static void MapTestTelemetryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/test-telemetry")
            .WithTags("Testing - Telemetry")
            .WithOpenApi();

        // Test endpoint that creates a manual Activity/Span
        group.MapGet("/create-span", async (ILogger<Program> logger) =>
        {
            using var activity = TestActivitySource.StartActivity("test-span-manual");

            activity?.SetTag("test.type", "manual");
            activity?.SetTag("test.timestamp", DateTimeOffset.UtcNow.ToString());

            logger.LogInformation("Manual span created for testing OpenTelemetry export");

            await Task.Delay(100); // Simulate some work

            return Results.Ok(new
            {
                message = "Manual span created",
                traceId = activity?.TraceId.ToString(),
                spanId = activity?.SpanId.ToString(),
                activityCreated = activity != null
            });
        })
        .WithName("CreateTestSpan")
        .WithSummary("Create a manual test span to verify OpenTelemetry export");
    }
}
