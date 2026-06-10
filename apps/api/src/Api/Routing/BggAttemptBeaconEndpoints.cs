using Api.Helpers;
using Api.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Api.Routing;

/// <summary>
/// Issue #2123 — anonymous beacon endpoint receiving notifications from the
/// FE custom Image loader (<c>apps/web/src/lib/images/safe-loader.ts</c>) when
/// a browser tried to render an image whose hostname matches the BGG block
/// list. The loader redirected to the placeholder, but the attempt itself
/// MUST be observed (SLO=0; any nonzero rate is a P1 incident).
/// </summary>
/// <remarks>
/// <para>
/// Posture: anonymous (no auth — would mask SSR / search-engine / signed-out
/// surfaces), POST-only (GET/HEAD/OPTIONS → 405), best-effort (always returns
/// 204 No Content even on parse failure — the beacon must NEVER be a reason
/// for the browser to retry).
/// </para>
/// <para>
/// Rate-limit: NONE intentionally. The beacon is server-cheap (one metric
/// increment + a structured log line) and rate-limiting it would mask the
/// signal precisely when it matters — under a sustained ToS-violation attack
/// surface. If runaway traffic ever becomes an issue, add edge-level rate
/// limiting (Cloudflare) rather than gating here.
/// </para>
/// </remarks>
internal static class BggAttemptBeaconEndpoints
{
    public static IEndpointRouteBuilder MapBggAttemptBeaconEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/metrics/bgg-attempt", (
            [FromBody] BggAttemptBeaconRequest? body,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Api.Routing.BggAttemptBeacon");
            ProcessBeacon(body, logger);
            return Results.NoContent();
        })
        .AllowAnonymous()
        .WithName("BggAttemptBeacon")
        .WithSummary("Beacon endpoint for the FE custom Image loader (issue #2123)")
        .WithDescription("Receives best-effort notifications when the FE Image loader caught a BGG-host src. Increments meepleai_bgg_url_attempted_render_total{path}.");

        return app;
    }

    /// <summary>
    /// Process logic split out for unit-test access — increments the Prometheus
    /// counter with the truncated <c>path</c> tag and logs a structured warning.
    /// </summary>
    /// <remarks>
    /// The beacon body is anonymous user input. Both <c>Path</c> and <c>Src</c>
    /// MUST be passed through <see cref="LogSanitizer.Sanitize(string?, int)"/>
    /// before reaching the log sink to neutralize CRLF injection / log forging
    /// (CodeQL <c>cs/log-forging</c>, ADR-058). The Prometheus tag is also
    /// truncated to bound cardinality.
    /// </remarks>
    internal static void ProcessBeacon(BggAttemptBeaconRequest? body, ILogger logger)
    {
        var rawPath = body?.Path;
        var path = string.IsNullOrWhiteSpace(rawPath) ? "unknown" : Truncate(rawPath!, 256);
        MeepleAiMetrics.BggUrlAttemptedRender.Add(
            1,
            new KeyValuePair<string, object?>("path", path));

        // Sanitize before logging: user input (Path, Src) must NOT reach the
        // log sink without LogSanitizer.Sanitize, which strips CR/LF and
        // bounds length. The counter `path` tag is already safe — it is
        // either the literal "unknown" or a Truncated string used as a
        // Prometheus label (CodeQL sanitizers-extensions barrier registered).
        var safePath = LogSanitizer.Sanitize(path, 256);
        var safeSrc = LogSanitizer.Sanitize(body?.Src ?? "(missing)", 256);
        logger.LogWarning(
            "BGG ToS violation attempt observed at path '{Path}' (src='{Src}'). " +
            "FE custom Image loader rewrote to placeholder. Issue #2123.",
            safePath,
            safeSrc);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}

/// <summary>
/// Request body for <c>POST /api/v1/metrics/bgg-attempt</c>.
/// All fields optional — the endpoint tolerates malformed payloads.
/// </summary>
internal sealed record BggAttemptBeaconRequest(string? Src, string? Path, long? Ts);
