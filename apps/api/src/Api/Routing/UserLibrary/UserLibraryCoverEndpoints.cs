using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// UserLibrary cover endpoints: upload and remove custom cover images (L3).
/// Issue #1824 (umbrella #1821, cover stack L3).
/// </summary>
internal static class UserLibraryCoverEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapUploadCustomCoverEndpoint(group);
        MapRemoveCustomCoverEndpoint(group);
    }

    private static void MapUploadCustomCoverEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/{gameId:guid}/cover", async (
            Guid gameId,
            IFormFile file,
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

            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "File is required" });
            }

            using var stream = file.OpenReadStream();
            var command = new UploadCustomCoverCommand(
                UserId: userId,
                GameId: gameId,
                FileStream: stream,
                FileSizeBytes: file.Length,
                MimeType: file.ContentType
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/library/{gameId}/cover", result);
            }
            catch (ForbiddenException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 403);
            }
        })
        .RequireAuthenticatedUser()
        .DisableAntiforgery()
        .Produces<CustomCoverUploadResult>(201)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .WithTags("Library")
        .WithSummary("Upload custom cover (L3)")
        .WithDescription("Uploads a user-custom cover image (200x300 webp ≤200KB) for a game in user's library.")
        .WithOpenApi();
    }

    private static void MapRemoveCustomCoverEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/{gameId:guid}/cover", async (
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

            var command = new RemoveCustomCoverCommand(userId, gameId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (ForbiddenException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 403);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Library")
        .WithSummary("Remove custom cover (L3)")
        .WithDescription("Removes the user's custom cover for a game in their library. Falls back to L4/L1 chain.")
        .WithOpenApi();
    }
}
