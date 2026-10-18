using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for embedding service monitoring.
/// Issue #4878: Embedding Service Dashboard
/// </summary>
internal static class AdminEmbeddingEndpoints
{
    public static RouteGroupBuilder MapAdminEmbeddingEndpoints(this RouteGroupBuilder group)
    {
        var embeddingGroup = group.MapGroup("/admin/embedding")
            .WithTags("Admin", "Embedding")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/embedding/info - Service info + health. #4446: through a query, so the
        // dimension is the API's and not a number written here.
        embeddingGroup.MapGet("/info", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetEmbeddingServiceInfoQuery(), ct).ConfigureAwait(false)));

        // GET /api/v1/admin/embedding/metrics - Prometheus metrics parsed to JSON
        embeddingGroup.MapGet("/metrics", async (
            IHttpClientFactory httpClientFactory,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                var client = httpClientFactory.CreateClient("EmbeddingService");
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var response = await client.GetAsync("/metrics", cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return Results.Ok(EmptyMetrics());
                }

                var text = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                return Results.Ok(ParsePrometheusMetrics(text));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to get embedding service metrics");
                return Results.Ok(EmptyMetrics());
            }
        });

        return group;
    }

    private static object EmptyMetrics() => new
    {
        requestsTotal = 0L,
        failuresTotal = 0L,
        durationMsSum = 0.0,
        totalCharsSum = 0.0,
        avgDurationMs = 0.0,
        failureRate = 0.0,
    };

    private static object ParsePrometheusMetrics(string text)
    {
        long requestsTotal = 0;
        long failuresTotal = 0;
        double durationMsSum = 0;
        double totalCharsSum = 0;

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

            if (string.Equals(key, "embed_requests_total", StringComparison.Ordinal) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var req))
            {
                requestsTotal = req;
            }
            else if (string.Equals(key, "embed_failures_total", StringComparison.Ordinal) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fail))
            {
                failuresTotal = fail;
            }
            else if (string.Equals(key, "embed_duration_ms_sum", StringComparison.Ordinal) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
            {
                durationMsSum = dur;
            }
            else if (string.Equals(key, "embed_total_chars_sum", StringComparison.Ordinal) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var chars))
            {
                totalCharsSum = chars;
            }
        }

        var avgDuration = requestsTotal > 0 ? durationMsSum / requestsTotal : 0.0;
        var failureRate = requestsTotal > 0 ? (double)failuresTotal / requestsTotal * 100.0 : 0.0;

        return new
        {
            requestsTotal,
            failuresTotal,
            durationMsSum = Math.Round(durationMsSum, 2),
            totalCharsSum = Math.Round(totalCharsSum, 0),
            avgDurationMs = Math.Round(avgDuration, 2),
            failureRate = Math.Round(failureRate, 2),
        };
    }
}
