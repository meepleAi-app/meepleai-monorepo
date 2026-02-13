using MediatR;
using Api.BoundedContexts.Administration.Application.Queries.CheckPermission;
using Api.BoundedContexts.Administration.Application.Queries.GetUserPermissions;
using Api.Extensions;

namespace Api.Routing;

/// <summary>
/// Permission API routes (Epic #4068 - Issue #4177)
/// </summary>
public static class PermissionRoutes
{
    public static IEndpointRouteBuilder MapPermissionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/permissions")
            .WithTags("Permissions")
            .RequireAuthorization();

        group.MapGet("/me", async (HttpContext ctx, IMediator m) =>
        {
            var userId = ctx.User.GetUserId();
            var result = await m.Send(new GetUserPermissionsQuery(userId)).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetMyPermissions")
        .WithSummary("Get current user's permissions")
        .Produces<GetUserPermissionsResponse>();

        group.MapGet("/check", async (HttpContext ctx, IMediator m, string feature, string? state = null) =>
        {
            var userId = ctx.User.GetUserId();
            var result = await m.Send(new CheckPermissionQuery(userId, feature, state)).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("CheckPermission")
        .WithSummary("Check feature access")
        .Produces<CheckPermissionResponse>();

        return app;
    }
}
