using Api.BoundedContexts.Administration.Application.Commands.Resources;
using Api.BoundedContexts.Administration.Application.Queries.Resources;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin resources monitoring endpoints for database, cache, and vector store metrics.
/// Issue #3695: Resources Monitoring - Database/Cache/Vectors
/// </summary>
internal static class AdminResourcesEndpoints
{
    public static RouteGroupBuilder MapAdminResourcesEndpoints(this RouteGroupBuilder group)
    {
        // Database Metrics
        group.MapGet("/resources/database/metrics", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            var metrics = await mediator.Send(new GetDatabaseMetricsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(metrics);
        })
        .WithName("GetDatabaseMetrics")
        .WithTags("Admin", "Resources")
        .Produces<Api.Models.DatabaseMetricsDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        group.MapGet("/resources/database/tables/top", async (
            HttpContext context,
            IMediator mediator,
            int limit = 10,
            CancellationToken ct = default) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            var tables = await mediator.Send(new GetTopTablesBySizeQuery(limit), ct).ConfigureAwait(false);
            return Results.Ok(tables);
        })
        .WithName("GetTopTablesBySize")
        .WithTags("Admin", "Resources")
        .Produces<IReadOnlyList<Api.Models.TableSizeDto>>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // Cache Metrics
        group.MapGet("/resources/cache/metrics", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            var metrics = await mediator.Send(new GetCacheMetricsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(metrics);
        })
        .WithName("GetCacheMetrics")
        .WithTags("Admin", "Resources")
        .Produces<Api.Models.CacheMetricsDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // Vector Store Metrics
        group.MapGet("/resources/vectors/metrics", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            var metrics = await mediator.Send(new GetVectorStoreMetricsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(metrics);
        })
        .WithName("GetVectorStoreMetrics")
        .WithTags("Admin", "Resources")
        .Produces<Api.Models.VectorStoreMetricsDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // Dangerous Actions

        // Clear Cache (Level 2: DANGER)
        group.MapPost("/resources/cache/clear", async (
            HttpContext context,
            IMediator mediator,
            bool confirmed = false,
            CancellationToken ct = default) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            try
            {
                var success = await mediator.Send(new ClearCacheCommand(confirmed), ct).ConfigureAwait(false);
                return Results.Ok(new { success, message = "Cache cleared successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ClearCache")
        .WithTags("Admin", "Resources", "Dangerous")
        .Produces<object>(200)
        .ProducesProblem(400)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // VACUUM Database (Level 1: WARNING)
        group.MapPost("/resources/database/vacuum", async (
            HttpContext context,
            IMediator mediator,
            bool confirmed = false,
            bool fullVacuum = false,
            CancellationToken ct = default) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            try
            {
                var success = await mediator.Send(new VacuumDatabaseCommand(confirmed, fullVacuum), ct).ConfigureAwait(false);
                var vacuumType = fullVacuum ? "VACUUM FULL" : "VACUUM";
                return Results.Ok(new { success, message = $"{vacuumType} executed successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("VacuumDatabase")
        .WithTags("Admin", "Resources", "Dangerous")
        .Produces<object>(200)
        .ProducesProblem(400)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // Rebuild Vector Index — disabled (Qdrant replaced by pgvector)
        group.MapPost("/resources/vectors/rebuild", (
            HttpContext context,
            string collectionName,
            bool confirmed = false) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            return Results.BadRequest(new { error = "Vector index rebuild not available — Qdrant replaced by pgvector" });
        })
        .WithName("RebuildVectorIndex")
        .WithTags("Admin", "Resources", "Dangerous")
        .Produces<object>(200)
        .ProducesProblem(400)
        .ProducesProblem(401)
        .ProducesProblem(403);

        return group;
    }
}
