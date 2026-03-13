using Api.BoundedContexts.Administration.Application.Queries.Docker;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin Docker container management endpoints (read-only).
/// Issue #139: Container management API.
/// </summary>
internal static class AdminDockerEndpoints
{
    public static RouteGroupBuilder MapAdminDockerEndpoints(this RouteGroupBuilder group)
    {
        var dockerGroup = group.MapGroup("/admin/docker")
            .WithTags("Admin", "Docker");

        // List all containers
        dockerGroup.MapGet("/containers", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetContainersQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("List Docker containers")
        .WithDescription("Returns all containers (running and stopped) via Docker Socket Proxy");

        // Get container logs
        dockerGroup.MapGet("/containers/{containerId}/logs", async (
            HttpContext context,
            IMediator mediator,
            string containerId,
            int? tail,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetContainerLogsQuery(containerId, tail ?? 100);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get container logs")
        .WithDescription("Returns recent log lines for a specific container");

        return group;
    }
}
