using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Extensions;
using Api.SharedKernel.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// GameBook catalog endpoints — Phase E1 of the gamebook multi-book generalization
/// (spec <c>docs/superpowers/specs/2026-05-19-gamebook-multi-book-generalization-design.md</c>).
///
/// Exposes the list of <c>GameBook</c> rows owned by a game (or by a specific user
/// for private games). Consumed by the photo-translate form to populate the
/// <c>BookPicker</c> when a game has 2+ narrative books, and by the play page
/// to drive per-book resume.
/// </summary>
internal static class GameBookEndpoints
{
    public static RouteGroupBuilder MapGameBookEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/gamebook/books", async (
            [FromQuery] Guid gameRefId,
            [FromQuery] int gameRefKind,
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

            if (gameRefId == Guid.Empty)
                return Results.BadRequest(new { error = "gameRefId is required" });
            if (!Enum.IsDefined(typeof(GameRefKind), gameRefKind))
                return Results.BadRequest(new { error = "gameRefKind must be 0 (Shared) or 1 (Private)" });

            var kind = (GameRefKind)gameRefKind;
            var gameRef = kind == GameRefKind.Shared
                ? GameRef.Shared(gameRefId)
                : GameRef.Private(gameRefId);

            // For private games, scope to the authenticated user; for shared games,
            // pass null so community books are returned regardless of caller.
            Guid? ownerFilter = kind == GameRefKind.Private ? userId : null;

            var result = await mediator
                .Send(new ListGameBooksByGameQuery(gameRef, ownerFilter), ct)
                .ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<GameBookDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithTags("Gamebook")
        .WithSummary("List GameBook rows for a given GameRef")
        .WithDescription(
            "Returns the catalog of game books (Tutorial, Setup, Narrative, Encounter, RulesReference) " +
            "for the specified GameRef. Used by BookPicker (photo-translate) and ResumeBooksList (play).")
        .WithOpenApi();

        return group;
    }

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null)
        {
            userId = session.Principal!.Subject.Id;
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
