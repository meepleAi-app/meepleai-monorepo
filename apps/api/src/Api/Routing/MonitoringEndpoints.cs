using Api.BoundedContexts.Administration.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Infrastructure monitoring endpoints (Admin only).
/// Issues #891 + #893: Health checks and Prometheus metrics queries.
/// </summary>
public static class MonitoringEndpoints
{
    public static RouteGroupBuilder MapMonitoringEndpoints(this RouteGroupBuilder group)
    {
        // Issue #891: Infrastructure health checks
        group.MapGet("/admin/infrastructure/health", async (
            HttpContext context,
            IMediator mediator,
            [FromQuery] string? service = null,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetInfrastructureHealthQuery { ServiceName = service };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Json(new
            {
                overall = new
                {
                    state = result.Overall.State.ToString(),
                    totalServices = result.Overall.TotalServices,
                    healthyServices = result.Overall.HealthyServices,
                    degradedServices = result.Overall.DegradedServices,
                    unhealthyServices = result.Overall.UnhealthyServices,
                    checkedAt = result.Overall.CheckedAt
                },
                services = result.Services.Select(s => new
                {
                    serviceName = s.ServiceName,
                    state = s.State,
                    errorMessage = s.ErrorMessage,
                    checkedAt = s.CheckedAt,
                    responseTimeMs = s.ResponseTimeMs
                })
            });
        })
        .WithName("GetInfrastructureHealth")
        .WithTags("Monitoring")
        .Produces<object>(200)
        .Produces(401);

        // Issue #893: Prometheus range queries
        group.MapGet("/admin/prometheus/metrics", async (
            HttpContext context,
            IMediator mediator,
            [FromQuery] string query,
            [FromQuery] DateTime start,
            [FromQuery] DateTime end,
            [FromQuery] string step = "5m",
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.BadRequest(new { error = "Query parameter is required" });
            }

            var metricsQuery = new GetPrometheusMetricsQuery
            {
                Query = query,
                Start = start,
                End = end,
                Step = step
            };

            var result = await mediator.Send(metricsQuery, ct).ConfigureAwait(false);

            return Results.Json(new
            {
                resultType = result.ResultType,
                timeSeries = result.TimeSeries.Select(ts => new
                {
                    metric = ts.Metric,
                    values = ts.Values.Select(v => new
                    {
                        timestamp = v.Timestamp,
                        value = v.Value
                    })
                })
            });
        })
        .WithName("GetPrometheusMetrics")
        .WithTags("Monitoring")
        .Produces<object>(200)
        .Produces(400)
        .Produces(401);

        return group;
    }
}
