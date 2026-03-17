using Api.Extensions;
using Api.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Routing;

/// <summary>
/// Independent status page endpoints that work even when the frontend is down.
/// GET /status — public HTML page with auto-refresh (rate-limited).
/// GET /status/details — JSON details (admin-only).
/// </summary>
internal static class StatusPageEndpoints
{
    /// <summary>
    /// Maps the /status and /status/details endpoints directly on the application root.
    /// </summary>
    public static void MapStatusPageEndpoints(this WebApplication app)
    {
        // GET /status → self-contained HTML page (no auth, rate-limited)
        app.MapGet("/status", async (HealthCheckService healthCheckService) =>
        {
            var report = await healthCheckService.CheckHealthAsync().ConfigureAwait(false);
            var html = StatusPageRenderer.RenderHtml(report);
            return Results.Content(html, "text/html");
        })
        .WithName("StatusPage")
        .WithTags("Status")
        .ExcludeFromDescription()
        .RequireRateLimiting("AuthLogin");

        // GET /status/details → JSON (admin session required)
        app.MapGet("/status/details", async (HealthCheckService healthCheckService) =>
        {
            var report = await healthCheckService.CheckHealthAsync().ConfigureAwait(false);
            var details = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration_ms = e.Value.Duration.TotalMilliseconds,
                description = e.Value.Description,
                tags = e.Value.Tags
            });
            return Results.Ok(new
            {
                overall = report.Status.ToString(),
                total_duration_ms = report.TotalDuration.TotalMilliseconds,
                services = details,
                timestamp = DateTime.UtcNow
            });
        })
        .WithName("StatusDetails")
        .WithTags("Status")
        .RequireAdminSession();
    }
}
