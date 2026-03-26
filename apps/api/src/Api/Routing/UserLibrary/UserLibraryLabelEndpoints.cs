using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.Labels;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// UserLibrary Label endpoints: label CRUD and game label assignment (Epic #3511).
/// </summary>
internal static class UserLibraryLabelEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapGetLabelsEndpoint(group);
        MapGetGameLabelsEndpoint(group);
        MapAddLabelToGameEndpoint(group);
        MapRemoveLabelFromGameEndpoint(group);
        MapCreateCustomLabelEndpoint(group);
        MapDeleteCustomLabelEndpoint(group);
    }

    private static void MapGetLabelsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/labels", async (
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

            var query = new GetLabelsQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<LabelDto>>(200)
        .Produces(401)
        .WithTags("Library", "Labels")
        .WithSummary("Get available labels")
        .WithDescription("Returns all labels available to the user (predefined system labels + user's custom labels).")
        .WithOpenApi();
    }

    private static void MapGetGameLabelsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/library/games/{gameId:guid}/labels", async (
            Guid gameId,
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

            var query = new GetGameLabelsQuery(userId, gameId);

            try
            {
                var result = await mediator.Send(query, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<LabelDto>>(200)
        .Produces(401)
        .Produces(404)
        .WithTags("Library", "Labels")
        .WithSummary("Get game labels")
        .WithDescription("Returns labels assigned to a specific game in user's library.")
        .WithOpenApi();
    }

    private static void MapAddLabelToGameEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/games/{gameId:guid}/labels/{labelId:guid}", async (
            Guid gameId,
            Guid labelId,
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

            var command = new AddLabelToGameCommand(userId, gameId, labelId);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<bool>(200)
        .Produces(401)
        .Produces(404)
        .Produces(409)
        .WithTags("Library", "Labels")
        .WithSummary("Add label to game")
        .WithDescription("Assigns a label to a game in user's library. Returns 409 if label is already assigned.")
        .WithOpenApi();
    }

    private static void MapRemoveLabelFromGameEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/games/{gameId:guid}/labels/{labelId:guid}", async (
            Guid gameId,
            Guid labelId,
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

            var command = new RemoveLabelFromGameCommand(userId, gameId, labelId);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return result ? Results.NoContent() : Results.NotFound(new { error = "Label not assigned to this game" });
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .WithTags("Library", "Labels")
        .WithSummary("Remove label from game")
        .WithDescription("Removes a label from a game in user's library.")
        .WithOpenApi();
    }

    private static void MapCreateCustomLabelEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/labels", async (
            [Microsoft.AspNetCore.Mvc.FromBody] CreateCustomLabelRequest request,
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

            var command = new CreateCustomLabelCommand(userId, request.Name, request.Color);

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/library/labels/{result.Id}", result);
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<LabelDto>(201)
        .Produces(401)
        .Produces(409)
        .WithTags("Library", "Labels")
        .WithSummary("Create custom label")
        .WithDescription("Creates a new custom label for the user. Returns 409 if label name already exists.")
        .WithOpenApi();
    }

    private static void MapDeleteCustomLabelEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/labels/{labelId:guid}", async (
            Guid labelId,
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

            var command = new DeleteCustomLabelCommand(userId, labelId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(404)
        .Produces(409)
        .WithTags("Library", "Labels")
        .WithSummary("Delete custom label")
        .WithDescription("Deletes a custom label. Returns 404 if not found or not owned. Returns 409 if trying to delete predefined label.")
        .WithOpenApi();
    }
}
