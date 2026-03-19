using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.Extensions;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Database sync endpoints for staging-to-local and local-to-staging data synchronization.
/// SuperAdmin only with Features.DatabaseSync feature flag gate.
/// </summary>
internal static class DatabaseSyncEndpoints
{
    public static RouteGroupBuilder MapDatabaseSyncEndpoints(this RouteGroupBuilder group)
    {
        var syncGroup = group.MapGroup("/admin/database-sync")
            .RequireAuthorization("RequireSuperAdmin")
            .WithTags("Admin", "DatabaseSync");

        // -- Tunnel Management -----------------------------------------------

        syncGroup.MapGet("/tunnel/status", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var result = await mediator.Send(new GetTunnelStatusQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Get SSH tunnel status")
        .WithDescription("Returns current state of the SSH tunnel to the staging database");

        syncGroup.MapPost("/tunnel/open", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var result = await mediator.Send(new OpenTunnelCommand(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Open SSH tunnel to staging database")
        .WithDescription("Establishes an SSH tunnel connection to the staging database server");

        syncGroup.MapDelete("/tunnel/close", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var result = await mediator.Send(new CloseTunnelCommand(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Close SSH tunnel")
        .WithDescription("Closes the active SSH tunnel connection to the staging database");

        // -- Schema Operations -----------------------------------------------

        syncGroup.MapGet("/schema/compare", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var result = await mediator.Send(new CompareSchemaQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Compare local and staging schemas")
        .WithDescription("Returns a diff of database schemas between local and staging environments");

        syncGroup.MapPost("/schema/preview-sql", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var result = await mediator.Send(new PreviewMigrationSqlCommand(), ct).ConfigureAwait(false);
            return Results.Ok(new { sql = result });
        })
        .WithSummary("Preview migration SQL")
        .WithDescription("Generates the SQL that would be executed to sync schemas without applying it");

        syncGroup.MapPost("/schema/apply", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            [FromBody] ApplyMigrationsRequest request,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var command = new ApplyMigrationsCommand(request.Direction, request.Confirmation, session!.User!.Id);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Apply schema migrations")
        .WithDescription("Applies pending schema migrations in the specified direction. Requires confirmation string.");

        // -- Table Data Operations -------------------------------------------

        syncGroup.MapGet("/tables", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var result = await mediator.Send(new ListTablesQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("List all tables with row counts")
        .WithDescription("Returns all tables in both local and staging databases with their row counts");

        syncGroup.MapGet("/tables/{name}/compare", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            [FromRoute] string name,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var result = await mediator.Send(new CompareTableDataQuery(name), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Compare table data between environments")
        .WithDescription("Returns a row-level diff for the specified table between local and staging");

        syncGroup.MapPost("/tables/{name}/sync", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            [FromRoute] string name,
            [FromBody] SyncTableDataRequest request,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var command = new SyncTableDataCommand(name, request.Direction, request.Confirmation, session!.User!.Id);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Sync table data")
        .WithDescription("Synchronizes data for the specified table in the given direction. Requires confirmation string.");

        // -- Operations History ----------------------------------------------

        syncGroup.MapGet("/operations/history", async (
            HttpContext context,
            IMediator mediator,
            IFeatureFlagService featureFlags,
            [FromQuery] int limit = 50,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            if (!await featureFlags.IsEnabledAsync("Features.DatabaseSync").ConfigureAwait(false))
                return Results.Json(new { error = "Database sync feature is disabled" }, statusCode: 403);

            var result = await mediator.Send(new GetSyncOperationsHistoryQuery(limit), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithSummary("Get sync operations history")
        .WithDescription("Returns recent sync operations with their results, ordered by most recent first");

        return group;
    }
}

// Request DTOs
internal record ApplyMigrationsRequest(SyncDirection Direction, string Confirmation);
internal record SyncTableDataRequest(SyncDirection Direction, string Confirmation);
