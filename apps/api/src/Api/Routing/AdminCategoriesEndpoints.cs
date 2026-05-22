using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AdminCategories;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.Extensions;
using Api.Helpers;
using Api.SharedKernel.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin CRUD endpoints for SharedGame categories.
/// Issue #1440 — Phase 2 BE wiring for the categories management surface.
/// Phase 1 (PR #1430) shipped FE-only dialogs with hardcoded mock data.
/// </summary>
internal static class AdminCategoriesEndpoints
{
    /// <summary>
    /// Request payload for creating a new category.
    /// </summary>
    /// <param name="Name">Display name (1..50 chars, unique).</param>
    /// <param name="Slug">URL-friendly slug (lowercase alphanumeric + hyphens, unique).</param>
    /// <param name="Emoji">Optional emoji glyph used as visual marker.</param>
    /// <param name="Color">Optional 6-digit hex color (e.g. #ef4444).</param>
    public sealed record CreateCategoryRequest(string Name, string Slug, string? Emoji, string? Color);

    /// <summary>
    /// Request payload for updating an existing category. Same shape as create.
    /// </summary>
    public sealed record UpdateCategoryRequest(string Name, string Slug, string? Emoji, string? Color);

    public static RouteGroupBuilder MapAdminCategoriesEndpoints(this RouteGroupBuilder group)
    {
        var categoriesGroup = group.MapGroup("/admin/categories")
            .WithTags("Admin", "Categories");

        categoriesGroup.MapGet("/", HandleGetCategories)
            .RequireAdminSession()
            .WithName("AdminGetCategories")
            .WithSummary("Admin: list all SharedGame categories with derived gameCount")
            .Produces<List<GameCategoryDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        categoriesGroup.MapPost("/", HandleCreateCategory)
            .RequireAdminSession()
            .WithName("AdminCreateCategory")
            .WithSummary("Admin: create a new SharedGame category")
            .Produces<GameCategoryDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status409Conflict);

        categoriesGroup.MapPut("/{id:guid}", HandleUpdateCategory)
            .RequireAdminSession()
            .WithName("AdminUpdateCategory")
            .WithSummary("Admin: update an existing SharedGame category")
            .Produces<GameCategoryDto>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        categoriesGroup.MapDelete("/{id:guid}", HandleDeleteCategory)
            .RequireAdminSession()
            .WithName("AdminDeleteCategory")
            .WithSummary("Admin: delete a SharedGame category (forbidden when gameCount > 0)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<IResult> HandleGetCategories(
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAdminCategoriesQuery();
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCreateCategory(
        [FromBody] CreateCategoryRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} creating category {Name}",
            session!.User!.Id,
            LogSanitizer.Sanitize(request.Name));

        var command = new CreateGameCategoryCommand(
            Name: request.Name,
            Slug: request.Slug,
            Emoji: request.Emoji,
            Color: request.Color,
            ActorUserId: session.User!.Id);

        var category = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Created($"/api/v1/admin/categories/{category.Id}", category);
    }

    private static async Task<IResult> HandleUpdateCategory(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} updating category {Id}",
            session!.User!.Id,
            id);

        var command = new UpdateGameCategoryCommand(
            Id: id,
            Name: request.Name,
            Slug: request.Slug,
            Emoji: request.Emoji,
            Color: request.Color,
            ActorUserId: session.User!.Id);

        var category = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.Ok(category);
    }

    private static async Task<IResult> HandleDeleteCategory(
        Guid id,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} deleting category {Id}",
            session!.User!.Id,
            id);

        var command = new DeleteGameCategoryCommand(Id: id, ActorUserId: session.User!.Id);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}
