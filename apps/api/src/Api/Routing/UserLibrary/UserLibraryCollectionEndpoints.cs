using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Extensions;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// UserLibrary Collection endpoints: generic collection status, add/remove, and bulk operations (Issues #4263, #4268).
/// </summary>
internal static class UserLibraryCollectionEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        // Generic collection endpoints (Issue #4263)
        MapGetCollectionStatusEndpoint(group);
        MapAddToCollectionEndpoint(group);
        MapRemoveFromCollectionEndpoint(group);

        // Bulk collection endpoints (Issue #4268)
        MapBulkAddToCollectionEndpoint(group);
        MapBulkRemoveFromCollectionEndpoint(group);
        MapBulkGetCollectionAssociatedDataEndpoint(group);
    }

    private static void MapGetCollectionStatusEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/collections/{entityType}/{entityId:guid}/status", async (
            string entityType,
            Guid entityId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var query = new GetCollectionStatusQuery(userId, parsedEntityType, entityId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<CollectionStatusDto>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("Collections")
        .WithSummary("Check collection status")
        .WithDescription("Returns whether an entity is in user's collection with associated data counts. Issue #4263.")
        .WithOpenApi();
    }

    private static void MapAddToCollectionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/collections/{entityType}/{entityId:guid}", async (
            string entityType,
            Guid entityId,
            [FromBody] AddToCollectionRequest? request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var command = new AddToCollectionCommand(
                UserId: userId,
                EntityType: parsedEntityType,
                EntityId: entityId,
                IsFavorite: request?.IsFavorite ?? false,
                Notes: request?.Notes
            );

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/collections/{entityType}/{entityId}/status",
                    new { message = "Entity added to collection" });
            }
            catch (DomainException ex) when (ex.Message.Contains("already in"))
            {
                return Results.Conflict(new { error = "Entity is already in collection" });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(201)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .Produces(409)
        .WithTags("Collections")
        .WithSummary("Add entity to collection")
        .WithDescription("Adds an entity to user's collection with optional favorite status and notes. Returns 409 if already in collection. Issue #4263.")
        .WithOpenApi();
    }

    private static void MapRemoveFromCollectionEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/collections/{entityType}/{entityId:guid}", async (
            string entityType,
            Guid entityId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var command = new RemoveFromCollectionCommand(userId, parsedEntityType, entityId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (DomainException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(400)
        .Produces(401)
        .Produces(404)
        .WithTags("Collections")
        .WithSummary("Remove entity from collection")
        .WithDescription("Removes an entity from user's collection. Returns 404 if not in collection. Issue #4263.")
        .WithOpenApi();
    }

    private static void MapBulkAddToCollectionEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/collections/{entityType}/bulk-add", async (
            string entityType,
            [FromBody] BulkAddToCollectionRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var command = new BulkAddToCollectionCommand(
                UserId: userId,
                EntityType: parsedEntityType,
                EntityIds: request.EntityIds,
                IsFavorite: request.IsFavorite,
                Notes: request.Notes
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<BulkOperationResult>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("Collections", "Bulk")
        .WithSummary("Bulk add entities to collection")
        .WithDescription("Adds multiple entities to user's collection. Uses partial success pattern. Max 50 entities. Issue #4268.")
        .WithOpenApi();
    }

    private static void MapBulkRemoveFromCollectionEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/collections/{entityType}/bulk-remove", async (
            string entityType,
            [FromBody] BulkRemoveFromCollectionRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var command = new BulkRemoveFromCollectionCommand(
                UserId: userId,
                EntityType: parsedEntityType,
                EntityIds: request.EntityIds
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<BulkOperationResult>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("Collections", "Bulk")
        .WithSummary("Bulk remove entities from collection")
        .WithDescription("Removes multiple entities from user's collection. Uses partial success pattern. Max 50 entities. Issue #4268.")
        .WithOpenApi();
    }

    private static void MapBulkGetCollectionAssociatedDataEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/collections/{entityType}/bulk-associated-data", async (
            string entityType,
            [FromBody] BulkGetAssociatedDataRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            // Parse entityType string to enum
            if (!Enum.TryParse<EntityType>(entityType, ignoreCase: true, out var parsedEntityType))
            {
                return Results.BadRequest(new { error = $"Invalid entity type: {entityType}" });
            }

            var query = new GetBulkCollectionAssociatedDataQuery(
                UserId: userId,
                EntityType: parsedEntityType,
                EntityIds: request.EntityIds
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<BulkAssociatedDataDto>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("Collections", "Bulk")
        .WithSummary("Get bulk associated data")
        .WithDescription("Returns aggregated counts of associated data for multiple collection entries. Used for bulk removal warnings. Issue #4268.")
        .WithOpenApi();
    }
}
