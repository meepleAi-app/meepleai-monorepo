using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddGameTranslation;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.DeleteGameTranslation;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.UpdateGameTranslation;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslationByLocale;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameTranslations;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin CRUD endpoints for SharedGame translations.
/// Issue #2339 sub-PR 1/3 Wave 5 (Task 14): wires the existing CQRS commands
/// (Add/Update/Delete) + queries (GetAll/GetByLocale) shipped via PR #2370 to
/// an HTTP surface under <c>/api/v1/admin/games/{gameId}/translations</c>.
/// </summary>
internal static class SharedGameTranslationEndpoints
{
    /// <summary>Request payload for POST — admin adds a new translation row.</summary>
    public sealed record AddTranslationRequest(string Locale, string Title, string? Description, string Source);

    /// <summary>Request payload for PUT — updates existing translation; xmin captured client-side from GET.</summary>
    public sealed record UpdateTranslationRequest(string Title, string? Description, uint Xmin);

    /// <summary>Request payload for DELETE body — xmin captured client-side from GET.</summary>
    public sealed record DeleteTranslationRequest(uint Xmin);

    public static RouteGroupBuilder MapSharedGameTranslationEndpoints(this RouteGroupBuilder group)
    {
        var translationsGroup = group.MapGroup("/admin/games/{gameId:guid}/translations")
            .WithTags("Admin", "SharedGame Translations");

        translationsGroup.MapPost("/", HandleAddTranslation)
            .RequireAdminSession()
            .WithName("AdminAddGameTranslation")
            .WithSummary("Admin: add a new non-EN translation to a shared game")
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        translationsGroup.MapGet("/", HandleGetTranslations)
            .RequireAdminSession()
            .WithName("AdminGetGameTranslations")
            .WithSummary("Admin: list all active translations for a shared game")
            .Produces<IReadOnlyList<SharedGameTranslationDetailDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        translationsGroup.MapGet("/{locale}", HandleGetTranslationByLocale)
            .RequireAdminSession()
            .WithName("AdminGetGameTranslationByLocale")
            .WithSummary("Admin: fetch a single translation by locale (includes xmin for concurrency)")
            .Produces<SharedGameTranslationDetailDto>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        translationsGroup.MapPut("/{locale}", HandleUpdateTranslation)
            .RequireAdminSession()
            .WithName("AdminUpdateGameTranslation")
            .WithSummary("Admin: update an existing translation (optimistic concurrency via xmin)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        translationsGroup.MapDelete("/{locale}", HandleDeleteTranslation)
            .RequireAdminSession()
            .WithName("AdminDeleteGameTranslation")
            .WithSummary("Admin: soft-delete a translation (optimistic concurrency via xmin)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> HandleAddTranslation(
        Guid gameId,
        [FromBody] AddTranslationRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new AddGameTranslationCommand(
            GameId: gameId,
            Locale: request.Locale,
            Title: request.Title,
            Description: request.Description,
            Source: request.Source,
            ActorUserId: session!.Principal!.Subject.Id);

        var id = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Created(
            $"/api/v1/admin/games/{gameId}/translations/{request.Locale}",
            new { id });
    }

    private static async Task<IResult> HandleGetTranslations(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(new GetGameTranslationsQuery(gameId), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetTranslationByLocale(
        Guid gameId,
        string locale,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(new GetGameTranslationByLocaleQuery(gameId, locale), cancellationToken).ConfigureAwait(false);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateTranslation(
        Guid gameId,
        string locale,
        [FromBody] UpdateTranslationRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new UpdateGameTranslationCommand(
            GameId: gameId,
            Locale: locale,
            Title: request.Title,
            Description: request.Description,
            Xmin: request.Xmin,
            ActorUserId: session!.Principal!.Subject.Id);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleDeleteTranslation(
        Guid gameId,
        string locale,
        [FromBody] DeleteTranslationRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new DeleteGameTranslationCommand(
            GameId: gameId,
            Locale: locale,
            Xmin: request.Xmin,
            ActorUserId: session!.Principal!.Subject.Id);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}
