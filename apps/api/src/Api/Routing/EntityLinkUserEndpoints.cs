using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Application.DTOs;
using Api.BoundedContexts.EntityRelationships.Application.Queries;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// User-facing entity link endpoints (Issue #5137).
/// All routes require authentication. Scope is always User.
/// </summary>
internal static class EntityLinkUserEndpoints
{
    public static RouteGroupBuilder MapEntityLinkUserEndpoints(this RouteGroupBuilder group)
    {
        MapGetEntityLinksEndpoint(group);
        MapGetEntityLinkCountEndpoint(group);
        MapCreateEntityLinkEndpoint(group);
        MapDeleteEntityLinkEndpoint(group);
        return group;
    }

    // GET /library/entity-links?entityType=Game&entityId={}&linkType=expansion_of&targetEntityType=KbCard
    private static void MapGetEntityLinksEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/entity-links", async (
            [FromQuery] string entityType,
            [FromQuery] Guid entityId,
            [FromQuery] string? linkType,
            [FromQuery] string? targetEntityType,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            if (!Enum.TryParse<MeepleEntityType>(entityType, ignoreCase: true, out var parsedEntityType))
                return Results.BadRequest(new { error = $"Invalid entityType: {entityType}" });

            EntityLinkType? parsedLinkType = null;
            if (!string.IsNullOrEmpty(linkType))
            {
                if (!Enum.TryParse<EntityLinkType>(linkType, ignoreCase: true, out var lt))
                    return Results.BadRequest(new { error = $"Invalid linkType: {linkType}" });
                parsedLinkType = lt;
            }

            // Issue #5188: optional TargetEntityType filter (e.g. KbCard for KB document links)
            MeepleEntityType? parsedTargetEntityType = null;
            if (!string.IsNullOrEmpty(targetEntityType))
            {
                if (!Enum.TryParse<MeepleEntityType>(targetEntityType, ignoreCase: true, out var tet))
                    return Results.BadRequest(new { error = $"Invalid targetEntityType: {targetEntityType}" });
                parsedTargetEntityType = tet;
            }

            var query = new GetEntityLinksQuery(
                EntityType: parsedEntityType,
                EntityId: entityId,
                RequestingUserId: userId,
                Scope: EntityLinkScope.User,
                LinkType: parsedLinkType,
                TargetEntityType: parsedTargetEntityType);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<EntityLinkDto>>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("EntityLinks")
        .WithSummary("Get entity links (user scope)")
        .WithDescription("Returns all user-scope links where the entity appears as source or target of a bidirectional link. Supports optional targetEntityType filter (Issue #5188). Issue #5137.")
        .WithOpenApi();
    }

    private static void MapGetEntityLinkCountEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/entity-links/count", async (
            [FromQuery] string entityType,
            [FromQuery] Guid entityId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            if (!Enum.TryParse<MeepleEntityType>(entityType, ignoreCase: true, out var parsedEntityType))
                return Results.BadRequest(new { error = $"Invalid entityType: {entityType}" });

            var query = new GetEntityLinkCountQuery(parsedEntityType, entityId);
            var count = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(new { count });
        })
        .RequireAuthenticatedUser()
        .Produces<object>(200)
        .Produces(400)
        .Produces(401)
        .WithTags("EntityLinks")
        .WithSummary("Get entity link count (badge)")
        .WithDescription("Returns the total count of links for an entity (as source or bidirectional target). Used by MeepleCard badge. Issue #5137.")
        .WithOpenApi();
    }

    // POST /library/entity-links
    private static void MapCreateEntityLinkEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/entity-links", async (
            [FromBody] CreateEntityLinkRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var command = new CreateEntityLinkCommand(
                SourceEntityType: request.SourceEntityType,
                SourceEntityId: request.SourceEntityId,
                TargetEntityType: request.TargetEntityType,
                TargetEntityId: request.TargetEntityId,
                LinkType: request.LinkType,
                Scope: EntityLinkScope.User,
                OwnerUserId: userId,
                Metadata: request.Metadata,
                IsBggImported: false);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/library/entity-links/{result.Id}", result);
            }
            catch (DuplicateEntityLinkException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<EntityLinkDto>(201)
        .Produces(400)
        .Produces(401)
        .Produces(409)
        .WithTags("EntityLinks")
        .WithSummary("Create entity link (user scope)")
        .WithDescription("Creates a new user-scope link between two entities. Scope=User, IsAdminApproved=true automatically (BR-04). Returns 409 for duplicates. Issue #5137.")
        .WithOpenApi();
    }

    // DELETE /library/entity-links/{linkId}
    private static void MapDeleteEntityLinkEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/entity-links/{linkId:guid}", async (
            Guid linkId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var userId = context.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();

            var command = new DeleteEntityLinkCommand(
                EntityLinkId: linkId,
                RequestingUserId: userId,
                IsAdmin: false);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (EntityLinkNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedEntityLinkAccessException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Forbidden");
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("EntityLinks")
        .WithSummary("Delete entity link")
        .WithDescription("Soft-deletes an entity link. Only the owner can delete. BGG-imported links cannot be deleted by users. Issue #5137.")
        .WithOpenApi();
    }
}

/// <summary>
/// Request body for creating a user-scope entity link (Issue #5137).
/// </summary>
public record CreateEntityLinkRequest(
    MeepleEntityType SourceEntityType,
    Guid SourceEntityId,
    MeepleEntityType TargetEntityType,
    Guid TargetEntityId,
    EntityLinkType LinkType,
    string? Metadata = null
);
