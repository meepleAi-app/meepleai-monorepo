using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Application.Queries.Infrastructure;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin infrastructure dashboard endpoints for AI service monitoring and control.
/// AI Infrastructure Dashboard: Service status, dependencies, config, pipeline test, restart, health-check.
/// </summary>
internal static class AdminInfrastructureEndpoints
{
    public static RouteGroupBuilder MapAdminInfrastructureEndpoints(this RouteGroupBuilder group)
    {
        var infraGroup = group.MapGroup("/admin/infrastructure")
            .WithTags("Admin", "Infrastructure");

        // GET /admin/infrastructure/services — AI services status overview
        infraGroup.MapGet("/services", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetAiServicesStatusQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get AI services status overview")
        .WithDescription("Returns health, uptime, and cooldown status for all AI infrastructure services")
        .Produces(200).Produces(401);

        // GET /admin/infrastructure/services/{name}/dependencies — Service dependency graph
        infraGroup.MapGet("/services/{name}/dependencies", async (
            string name,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetServiceDependenciesQuery(name), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get service dependency graph")
        .WithDescription("Returns upstream and downstream dependencies for a specific service")
        .Produces(200).Produces(401);

        // GET /admin/infrastructure/services/{name}/config — Service configuration
        infraGroup.MapGet("/services/{name}/config", async (
            string name,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetServiceConfigQuery(name), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get service configuration")
        .WithDescription("Returns current configuration and available parameters for a specific AI service")
        .Produces(200).Produces(401);

        // GET /admin/infrastructure/pipeline/test — End-to-end pipeline connectivity test
        infraGroup.MapGet("/pipeline/test", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new TestPipelineConnectivityQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Test pipeline connectivity")
        .WithDescription("Runs end-to-end connectivity test across the RAG pipeline chain")
        .Produces(200).Produces(401);

        // POST /admin/infrastructure/services/{name}/restart — Restart AI service (SuperAdmin only)
        infraGroup.MapPost("/services/{name}/restart", async (
            string name,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new RestartInfraServiceCommand(name), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("Restart AI service")
        .WithDescription("Triggers container restart for a specific AI service. SuperAdmin only, subject to cooldown.")
        .Produces(200).Produces(401).Produces(403);

        // POST /admin/infrastructure/services/{name}/health-check — Trigger health check (SuperAdmin only)
        infraGroup.MapPost("/services/{name}/health-check", async (
            string name,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new TriggerHealthCheckCommand(name), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("Trigger service health check")
        .WithDescription("Forces an immediate health check for a specific AI service. SuperAdmin only.")
        .Produces(200).Produces(401).Produces(403);

        // PUT /admin/infrastructure/services/{name}/config — Update service configuration (SuperAdmin only)
        infraGroup.MapPut("/services/{name}/config", async (
            string name,
            Dictionary<string, string> parameters,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new UpdateServiceConfigCommand(name, parameters), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("Update service configuration")
        .WithDescription("Updates runtime configuration parameters for a specific AI service. SuperAdmin only.")
        .Produces(200).Produces(401).Produces(403);

        return group;
    }
}
