using Api.Extensions;
using Api.Models;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Audit log and request tracking endpoints (Admin only).
/// Handles AI request logs, audit trail retrieval, and request filtering.
/// </summary>
public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/logs", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new Api.BoundedContexts.Administration.Application.Queries.GetAiRequestsQuery(Limit: 100);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            var response = result.Requests
                .Select(log =>
                {
                    var level = string.Equals(log.Status, "Error", StringComparison.OrdinalIgnoreCase)
                        ? "ERROR"
                        : "INFO";

                    var message = !string.IsNullOrWhiteSpace(log.ResponseSnippet)
                        ? log.ResponseSnippet!
                        : !string.IsNullOrWhiteSpace(log.Query)
                            ? log.Query!
                            : $"{log.Endpoint} request ({log.Status})";

                    return new LogEntryResponse(
                        log.CreatedAt,
                        level,
                        message,
                        log.Id.ToString(),
                        log.UserId?.ToString(),
                        log.GameId?.ToString()
                    );
                })
                .ToList();

            return Results.Json(response);
        });

        return group;
    }
}
