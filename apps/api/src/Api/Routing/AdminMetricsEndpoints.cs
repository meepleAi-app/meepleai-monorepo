using Api.BoundedContexts.Administration.Application.Queries.Metrics;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints surfacing Prometheus metric metadata
/// (Issue #1840 SP5 F4-C7 Alerts re-skin).
///
/// <list type="bullet">
///   <item><c>GET /api/v1/admin/metrics/labels</c> — list of known metric names
///         used by the MetricSelector dropdown when admins create alert rules.</item>
/// </list>
///
/// All endpoints require an Admin or SuperAdmin session.
/// </summary>
internal static class AdminMetricsEndpoints
{
    public static IEndpointRouteBuilder MapAdminMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/metrics")
            .WithTags("Admin", "AdminMetrics");

        group.MapGet("/labels", async (IMediator mediator, CancellationToken ct) =>
            {
                var result = await mediator.Send(new GetPrometheusMetricLabelsQuery(), ct).ConfigureAwait(false);
                return Results.Ok(new
                {
                    labels = result.Labels,
                    isFallback = result.IsFallback,
                });
            })
            .RequireAdminSession()
            .WithName("AdminMetrics_GetLabels")
            .WithSummary("List Prometheus metric names (cached 60s, falls back to static list)")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
