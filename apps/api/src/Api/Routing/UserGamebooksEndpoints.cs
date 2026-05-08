using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.Gamebooks;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// User gamebooks endpoint for the SP6 libro game `/gamebook` index page
/// (Issue #869).
///
/// Replaces the v1 carryover stub `useGamebooks` in
/// `apps/web/src/hooks/queries/useGamebooks.ts` so the index renders real
/// data instead of fixture, allowing the routing fix from PR #867 to land
/// users on a real `/library/games/{gameId}/play` URL.
/// </summary>
internal static class UserGamebooksEndpoints
{
    public static RouteGroupBuilder MapUserGamebooksEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/gamebooks", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var result = await mediator
                .Send(new GetUserGamebooksQuery(userId), ct)
                .ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .Produces<IReadOnlyList<GamebookCardDataDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithName("GetUserGamebooks")
        .WithTags("Gamebooks")
        .WithOpenApi(operation =>
        {
            operation.Summary = "List the authenticated user's gamebooks";
            operation.Description = "Returns the gamebook cards displayed on /gamebook (SP6 libro game index). MVP scope: card metadata sourced from PrivateGame; counts and status placeholders deferred to follow-up.";
            return operation;
        });

        return group;
    }

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null)
        {
            userId = session.User!.Id;
            return true;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out userId))
        {
            return true;
        }

        return false;
    }
}
