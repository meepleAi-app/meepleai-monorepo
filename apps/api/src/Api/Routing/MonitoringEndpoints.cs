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

        // Issue #892: Individual service health endpoints
        group.MapGet("/health/postgresql", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetInfrastructureHealthQuery { ServiceName = "postgres" };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            var service = result.Services.FirstOrDefault();
            if (service == null)
                return Results.NotFound(new { error = "PostgreSQL health check not found" });

            return Results.Json(new
            {
                serviceName = service.ServiceName,
                state = service.State.ToString(),
                errorMessage = service.ErrorMessage,
                checkedAt = service.CheckedAt,
                responseTimeMs = service.ResponseTimeMs
            });
        })
        .WithName("GetPostgreSqlHealth")
        .WithTags("Monitoring", "Health")
        .Produces<object>(200)
        .Produces(401)
        .Produces(404);

        group.MapGet("/health/redis", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetInfrastructureHealthQuery { ServiceName = "redis" };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            var service = result.Services.FirstOrDefault();
            if (service == null)
                return Results.NotFound(new { error = "Redis health check not found" });

            return Results.Json(new
            {
                serviceName = service.ServiceName,
                state = service.State.ToString(),
                errorMessage = service.ErrorMessage,
                checkedAt = service.CheckedAt,
                responseTimeMs = service.ResponseTimeMs
            });
        })
        .WithName("GetRedisHealth")
        .WithTags("Monitoring", "Health")
        .Produces<object>(200)
        .Produces(401)
        .Produces(404);

        group.MapGet("/health/qdrant", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetInfrastructureHealthQuery { ServiceName = "qdrant" };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            var service = result.Services.FirstOrDefault();
            if (service == null)
                return Results.NotFound(new { error = "Qdrant health check not found" });

            return Results.Json(new
            {
                serviceName = service.ServiceName,
                state = service.State.ToString(),
                errorMessage = service.ErrorMessage,
                checkedAt = service.CheckedAt,
                responseTimeMs = service.ResponseTimeMs
            });
        })
        .WithName("GetQdrantHealth")
        .WithTags("Monitoring", "Health")
        .Produces<object>(200)
        .Produces(401)
        .Produces(404);

        group.MapGet("/health/n8n", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetInfrastructureHealthQuery { ServiceName = "n8n" };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            var service = result.Services.FirstOrDefault();
            if (service == null)
                return Results.NotFound(new { error = "n8n health check not found" });

            return Results.Json(new
            {
                serviceName = service.ServiceName,
                state = service.State.ToString(),
                errorMessage = service.ErrorMessage,
                checkedAt = service.CheckedAt,
                responseTimeMs = service.ResponseTimeMs
            });
        })
        .WithName("GetN8NHealth")
        .WithTags("Monitoring", "Health")
        .Produces<object>(200)
        .Produces(401)
        .Produces(404);

        group.MapGet("/health/hyperdx", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetInfrastructureHealthQuery { ServiceName = "hyperdx" };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            var service = result.Services.FirstOrDefault();
            if (service == null)
                return Results.NotFound(new { error = "HyperDX health check not found" });

            return Results.Json(new
            {
                serviceName = service.ServiceName,
                state = service.State.ToString(),
                errorMessage = service.ErrorMessage,
                checkedAt = service.CheckedAt,
                responseTimeMs = service.ResponseTimeMs
            });
        })
        .WithName("GetHyperDxHealth")
        .WithTags("Monitoring", "Health")
        .Produces<object>(200)
        .Produces(401)
        .Produces(404);

        // Issue #894: Comprehensive infrastructure details (health + metrics)
        group.MapGet("/admin/infrastructure/details", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetInfrastructureDetailsQuery();
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
                    state = s.State.ToString(),
                    errorMessage = s.ErrorMessage,
                    checkedAt = s.CheckedAt,
                    responseTimeMs = s.ResponseTime.TotalMilliseconds
                }),
                prometheusMetrics = new
                {
                    apiRequestsLast24h = result.Metrics.ApiRequestsLast24h,
                    avgLatencyMs = result.Metrics.AvgLatencyMs,
                    errorRate = result.Metrics.ErrorRate,
                    llmCostLast24h = result.Metrics.LlmCostLast24h
                }
            });
        })
        .WithName("GetInfrastructureDetails")
        .WithTags("Monitoring")
        .WithSummary("Get comprehensive infrastructure details including health checks and Prometheus metrics")
        .WithDescription("Issue #894: Returns aggregated infrastructure status combining service health and operational metrics from Prometheus")
        .Produces<object>(200)
        .Produces(401);

        return group;
    }
}
