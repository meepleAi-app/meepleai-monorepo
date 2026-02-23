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
/// Admin-facing entity link endpoints (Issue #5138).
/// All routes require Admin role. Admin can view/create/delete any scope.
/// </summary>
internal static class EntityLinkAdminEndpoints
{
    public static RouteGroupBuilder MapEntityLinkAdminEndpoints(this RouteGroupBuilder group)
    {
        MapAdminGetEntityLinksEndpoint(group);
        MapAdminCreateEntityLinkEndpoint(group);
        MapAdminDeleteEntityLinkEndpoint(group);
        return group;
    }

    // GET /admin/entity-links?sourceType=Game&sourceId={}&linkType=expansion_of
    private static void MapAdminGetEntityLinksEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/admin/entity-links", async (
            [FromQuery] string sourceType,
            [FromQuery] Guid sourceId,
            [FromQuery] string? linkType,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            if (!Enum.TryParse<MeepleEntityType>(sourceType, ignoreCase: true, out var parsedEntityType))
                return Results.BadRequest(new { error = $"Invalid sourceType: {sourceType}" });

            EntityLinkType? parsedLinkType = null;
            if (!string.IsNullOrEmpty(linkType))
            {
                if (!Enum.TryParse<EntityLinkType>(linkType, ignoreCase: true, out var lt))
                    return Results.BadRequest(new { error = $"Invalid linkType: {linkType}" });
                parsedLinkType = lt;
            }

            // Admin queries with no scope restriction (sees all scopes)
            var query = new GetEntityLinksQuery(
                EntityType: parsedEntityType,
                EntityId: sourceId,
                RequestingUserId: null,
                Scope: null,
                LinkType: parsedLinkType);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .Produces<IReadOnlyList<EntityLinkDto>>(200)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .WithTags("EntityLinks", "Admin")
        .WithSummary("Get entity links (admin — all scopes)")
        .WithDescription("Returns all links for an entity regardless of scope. Issue #5138.")
        .WithOpenApi();
    }

    // POST /admin/entity-links
    private static void MapAdminCreateEntityLinkEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/admin/entity-links", async (
            [FromBody] CreateEntityLinkRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var adminUserId = session!.User!.Id;

            var command = new CreateEntityLinkCommand(
                SourceEntityType: request.SourceEntityType,
                SourceEntityId: request.SourceEntityId,
                TargetEntityType: request.TargetEntityType,
                TargetEntityId: request.TargetEntityId,
                LinkType: request.LinkType,
                Scope: EntityLinkScope.Shared,
                OwnerUserId: adminUserId,
                Metadata: request.Metadata,
                IsBggImported: false);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/admin/entity-links/{result.Id}", result);
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
        .Produces<EntityLinkDto>(201)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .Produces(409)
        .WithTags("EntityLinks", "Admin")
        .WithSummary("Create entity link (admin — shared scope)")
        .WithDescription("Creates a Shared-scope link between two entities. Admin approval required. Issue #5138.")
        .WithOpenApi();
    }

    // DELETE /admin/entity-links/{linkId}
    private static void MapAdminDeleteEntityLinkEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/admin/entity-links/{linkId:guid}", async (
            Guid linkId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var adminUserId = session!.User!.Id;

            var command = new DeleteEntityLinkCommand(
                EntityLinkId: linkId,
                RequestingUserId: adminUserId,
                IsAdmin: true);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (EntityLinkNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("EntityLinks", "Admin")
        .WithSummary("Delete entity link (admin)")
        .WithDescription("Admin soft-deletes any entity link including BGG-imported ones. Issue #5138.")
        .WithOpenApi();
    }
}
