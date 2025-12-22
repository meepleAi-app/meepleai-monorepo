using Api.BoundedContexts.Administration.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Infrastructure monitoring endpoints (Admin only).
/// Issues #891 + #893: Health checks and Prometheus metrics queries.
/// </summary>
internal static class MonitoringEndpoints
{
    public static RouteGroupBuilder MapMonitoringEndpoints(this RouteGroupBuilder group)
    {
        // Issue #891, #894: Infrastructure health checks & details
        MapInfrastructureHealthEndpoints(group);
        // Issue #892: Individual service health endpoints
        MapServiceHealthEndpoints(group);

        return group;
    }

    private static void MapInfrastructureHealthEndpoints(RouteGroupBuilder group)
    {
        MapGetInfrastructureHealthEndpoint(group);
        MapGetInfrastructureDetailsEndpoint(group);
    }

    private static void MapGetInfrastructureHealthEndpoint(RouteGroupBuilder group)
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
    }

    private static void MapGetInfrastructureDetailsEndpoint(RouteGroupBuilder group)
    {
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
    }

    private static void MapServiceHealthEndpoints(RouteGroupBuilder group)
    {
        // Issue #892: Individual service health endpoints
        MapGenericServiceHealthEndpoint(group, "/health/postgresql", "postgres", "GetPostgreSqlHealth", "PostgreSQL");
        MapGenericServiceHealthEndpoint(group, "/health/redis", "redis", "GetRedisHealth", "Redis");
        MapGenericServiceHealthEndpoint(group, "/health/qdrant", "qdrant", "GetQdrantHealth", "Qdrant");
        MapGenericServiceHealthEndpoint(group, "/health/n8n", "n8n", "GetN8NHealth", "n8n");
        MapGenericServiceHealthEndpoint(group, "/health/hyperdx", "hyperdx", "GetHyperDxHealth", "HyperDX");
    }

    private static void MapGenericServiceHealthEndpoint(
        RouteGroupBuilder group,
        string route,
        string serviceName,
        string endpointName,
        string errorDisplayName)
    {
        group.MapGet(route, async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetInfrastructureHealthQuery { ServiceName = serviceName };
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            var service = result.Services.FirstOrDefault();
            if (service == null)
                return Results.NotFound(new { error = $"{errorDisplayName} health check not found" });

            return Results.Json(new
            {
                serviceName = service.ServiceName,
                state = service.State.ToString(),
                errorMessage = service.ErrorMessage,
                checkedAt = service.CheckedAt,
                responseTimeMs = service.ResponseTimeMs
            });
        })
        .WithName(endpointName)
        .WithTags("Monitoring", "Health")
        .Produces<object>(200)
        .Produces(401)
        .Produces(404);
    }
}
