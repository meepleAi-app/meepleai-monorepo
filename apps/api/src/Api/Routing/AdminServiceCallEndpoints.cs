using Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for querying service call history and aggregated statistics.
/// </summary>
internal static class AdminServiceCallEndpoints
{
    public static RouteGroupBuilder MapAdminServiceCallEndpoints(this RouteGroupBuilder group)
    {
        var callGroup = group.MapGroup("/admin/service-calls").WithTags("Admin", "ServiceCalls");

        callGroup.MapGet("/", async (
            string? service, bool? success, string? correlationId,
            DateTime? from, DateTime? to, long? minLatencyMs,
            int? page, int? pageSize,
            IMediator mediator, HttpContext httpContext, CancellationToken ct) =>
        {
            var (authorized, _, error) = httpContext.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetServiceCallsQuery(
                ServiceName: service, IsSuccess: success, CorrelationId: correlationId,
                From: from, To: to, MinLatencyMs: minLatencyMs,
                Page: page ?? 1, PageSize: pageSize ?? 50);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithSummary("Query service call history");

        callGroup.MapGet("/summary", async (
            string? period, IMediator mediator, HttpContext httpContext, CancellationToken ct) =>
        {
            var (authorized, _, error) = httpContext.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetServiceCallSummaryQuery(period ?? "24h"), ct).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithSummary("Get aggregated service call statistics");

        return group;
    }
}
