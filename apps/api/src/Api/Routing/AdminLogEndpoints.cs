using Api.BoundedContexts.Administration.Application.Queries.Logs;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin structured application log endpoints backed by Seq.
/// </summary>
internal static class AdminLogEndpoints
{
    public static RouteGroupBuilder MapAdminLogEndpoints(this RouteGroupBuilder group)
    {
        var logGroup = group.MapGroup("/admin/logs").WithTags("Admin", "Logs");

        logGroup.MapGet("/", async (
            string? search, string? level, string? source, string? correlationId,
            DateTime? from, DateTime? to, int? count, string? afterId,
            IMediator mediator, HttpContext httpContext, CancellationToken ct) =>
        {
            var (authorized, _, error) = httpContext.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetApplicationLogsQuery(
                Search: search, Level: level, Source: source, CorrelationId: correlationId,
                From: from, To: to, Count: count ?? 50, AfterId: afterId);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Query structured application logs from Seq");

        return group;
    }
}
