using Api.BoundedContexts.Administration.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// User activity log endpoints for Admin Dashboard.
/// Issue #4652: Admin Dashboard API Integration.
/// Alias for /admin/audit-log with user-friendly path.
/// </summary>
internal static class UserActivityEndpoints
{
    public static RouteGroupBuilder MapUserActivityEndpoints(this RouteGroupBuilder group)
    {
        var activityGroup = group.MapGroup("/admin/users")
            .WithTags("Admin", "Users", "Activity");

        activityGroup.MapGet("/activity-log", async (
            HttpContext context,
            IMediator mediator,
            int? page,
            int? pageSize,
            Guid? userId,
            string? actionType,
            DateTime? startDate,
            DateTime? endDate,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Convert page-based to offset-based pagination
            var limit = pageSize ?? 20;
            var offset = page.HasValue ? (page.Value - 1) * limit : 0;

            var query = new GetAuditLogsQuery(
                Limit: limit,
                Offset: offset,
                AdminUserId: userId,
                Action: actionType,
                Resource: null, // Not filtering by resource
                Result: null,   // Not filtering by result (show all)
                StartDate: startDate,
                EndDate: endDate);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            // Transform response to match frontend expectations
            var response = new
            {
                activities = result.Entries.Select(e => new
                {
                    id = e.Id,
                    timestamp = e.CreatedAt,
                    userId = e.AdminUserId,
                    userName = e.UserName ?? "Unknown User",
                    userEmail = e.UserEmail ?? "",
                    action = e.Action,
                    actionType = e.Action, // Map action to actionType
                    target = $"{e.Resource} {(e.ResourceId != null ? $"(ID: {e.ResourceId})" : "")}",
                    ipAddress = e.IpAddress ?? "0.0.0.0",
                    status = string.Equals(e.Result, "success", StringComparison.OrdinalIgnoreCase) ? "success" : "error"
                }),
                total = result.TotalCount,
                page = page ?? 1,
                pageSize = limit
            };

            return Results.Ok(response);
        })
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get user activity log";
            operation.Description = "Retrieve paginated user activity log with filters for Admin Dashboard";
            return operation;
        });

        return group;
    }
}
