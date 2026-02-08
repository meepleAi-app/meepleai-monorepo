using Api.BoundedContexts.Administration.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin audit log endpoints for viewing and exporting audit trail.
/// Issue #3691: Audit Log System.
/// </summary>
internal static class AdminAuditLogEndpoints
{
    public static RouteGroupBuilder MapAdminAuditLogEndpoints(this RouteGroupBuilder group)
    {
        var auditGroup = group.MapGroup("/admin/audit-log")
            .WithTags("Admin", "AuditLog");

        auditGroup.MapGet("/", async (
            HttpContext context,
            IMediator mediator,
            int? limit,
            int? offset,
            Guid? adminUserId,
            string? action,
            string? resource,
            string? result,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAuditLogsQuery(
                Limit: limit ?? 50,
                Offset: offset ?? 0,
                AdminUserId: adminUserId,
                Action: action,
                Resource: resource,
                Result: result,
                StartDate: startDate,
                EndDate: endDate);

            var queryResult = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(queryResult);
        });

        auditGroup.MapGet("/export", async (
            HttpContext context,
            IMediator mediator,
            Guid? adminUserId,
            string? action,
            string? resource,
            string? result,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new ExportAuditLogsQuery(
                AdminUserId: adminUserId,
                Action: action,
                Resource: resource,
                Result: result,
                StartDate: startDate,
                EndDate: endDate);

            var exportResult = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.File(exportResult.Content, exportResult.ContentType, exportResult.FileName);
        });

        return group;
    }
}
