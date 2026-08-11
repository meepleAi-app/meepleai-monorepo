using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Commands;
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
/// Exposes the <c>GameBook</c> catalog for a game (or a user's private game) plus
/// personal-book CRUD. Consumed by the photo-translate <c>BookPicker</c>, the play
/// page's per-book resume, and the SP6 libro-game onboarding book-manager
/// (issue #2619 — the 1..N book model FE adoption).
///
/// Book creation via the API is scoped to <b>personal</b> books (owned by the
/// authenticated user). Community/admin-managed books (<c>OwnerUserId = null</c>)
/// are seeded/administered separately and are out of scope for this user-facing
/// surface — the write endpoints below always stamp <c>OwnerUserId = userId</c>.
/// </summary>
internal static class GameBookEndpoints
{
    public static RouteGroupBuilder MapGameBookEndpoints(this RouteGroupBuilder group)
    {
        MapListBooksEndpoint(group);
        MapCreateBookEndpoint(group);
        MapUpdateBookEndpoint(group);
        MapDeleteBookEndpoint(group);

        return group;
    }

    private static void MapListBooksEndpoint(RouteGroupBuilder group)
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
    }

    private static void MapCreateBookEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/gamebook/books", async (
            [FromBody] CreateGameBookRequest body,
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

            if (body.GameRefId == Guid.Empty)
                return Results.BadRequest(new { error = "gameRefId is required" });
            if (!Enum.IsDefined(typeof(GameRefKind), body.GameRefKind))
                return Results.BadRequest(new { error = "gameRefKind must be 0 (Shared) or 1 (Private)" });

            var kind = (GameRefKind)body.GameRefKind;
            var gameRef = kind == GameRefKind.Shared
                ? GameRef.Shared(body.GameRefId)
                : GameRef.Private(body.GameRefId);

            // User-facing creation always produces a PERSONAL book owned by the caller.
            var dto = await mediator.Send(
                new CreateGameBookCommand(
                    gameRef,
                    OwnerUserId: userId,
                    body.DisplayName,
                    body.Roles,
                    body.ParagraphScheme,
                    body.Language,
                    body.SequentialRead,
                    body.KbSourceDocId,
                    body.PhysicalOnly,
                    RequestedBy: userId),
                ct).ConfigureAwait(false);

            return Results.Created($"/api/v1/gamebook/books/{dto.Id}", dto);
        })
        .RequireAuthenticatedUser()
        .Produces<GameBookDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status409Conflict)
        .WithTags("Gamebook")
        .WithSummary("Create a personal GameBook")
        .WithDescription(
            "Creates a personal GameBook (owned by the authenticated user) for the given GameRef with the " +
            "chosen roles, paragraph scheme, language and optional KB source PDF. Returns 400 on validation " +
            "failure (e.g. no role, physicalOnly+kbSourceDocId), 409 if the PDF is already linked to a community book.")
        .WithOpenApi();
    }

    private static void MapUpdateBookEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/gamebook/books/{bookId:guid}", async (
            Guid bookId,
            [FromBody] UpdateGameBookRequest body,
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

            var dto = await mediator.Send(
                new UpdateGameBookCommand(bookId, body.DisplayName, body.Roles, RequestedBy: userId),
                ct).ConfigureAwait(false);

            return Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<GameBookDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Rename a GameBook and update its roles")
        .WithDescription("Updates the display name and role flags of an existing GameBook. Returns 404 if the book does not exist.")
        .WithOpenApi();
    }

    private static void MapDeleteBookEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/gamebook/books/{bookId:guid}", async (
            Guid bookId,
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

            await mediator.Send(new SoftDeleteGameBookCommand(bookId, RequestedBy: userId), ct)
                .ConfigureAwait(false);

            return Results.NoContent();
        })
        .RequireAuthenticatedUser()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Soft-delete a GameBook")
        .WithDescription("Soft-deletes an existing GameBook (IsDeleted + DeletedAt). Returns 404 if the book does not exist.")
        .WithOpenApi();
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

/// <summary>Request body for <c>POST /api/v1/gamebook/books</c> (creates a personal book).</summary>
public sealed record CreateGameBookRequest(
    Guid GameRefId,
    int GameRefKind,
    string DisplayName,
    int Roles,
    int ParagraphScheme,
    string Language,
    bool SequentialRead,
    Guid? KbSourceDocId,
    bool PhysicalOnly);

/// <summary>Request body for <c>PUT /api/v1/gamebook/books/{bookId}</c>.</summary>
public sealed record UpdateGameBookRequest(
    string DisplayName,
    int Roles);
