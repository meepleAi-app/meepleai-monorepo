using Api.BoundedContexts.KnowledgeBase.Application.Commands.AdminStrategy;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AdminStrategy;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Strategy management.
/// Issue #5314: Replace placeholder endpoints with real CQRS handlers.
/// </summary>
internal static class AdminStrategyEndpoints
{
    public static void MapAdminStrategyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/strategies")
            .WithTags("Strategy", "Admin");

        // GET /api/v1/admin/strategies - List all strategies
        group.MapGet("/", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var strategies = await mediator.Send(new ListAdminStrategiesQuery(), ct).ConfigureAwait(false);
            return Results.Ok(strategies);
        })
        .WithName("AdminGetStrategies")
        .Produces<IReadOnlyList<AdminStrategyResult>>()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/strategies/{id} - Get strategy by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var strategy = await mediator.Send(new GetAdminStrategyByIdQuery(id), ct).ConfigureAwait(false);
            return strategy != null
                ? Results.Ok(strategy)
                : Results.NotFound(new { message = "Strategy not found" });
        })
        .WithName("AdminGetStrategyById")
        .Produces<AdminStrategyResult>()
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/strategies - Create new strategy
        group.MapPost("/", async (
            [FromBody] CreateAdminStrategyRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new CreateAdminStrategyCommand(
                request.Name,
                request.Description ?? string.Empty,
                request.StepsJson,
                session!.User!.Id);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/admin/strategies/{result.Id}", result);
        })
        .WithName("AdminCreateStrategy")
        .Produces<AdminStrategyResult>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/strategies/{id} - Update strategy
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAdminStrategyRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            try
            {
                var command = new UpdateAdminStrategyCommand(
                    id,
                    request.Name,
                    request.Description ?? string.Empty,
                    request.StepsJson,
                    session!.User!.Id);

                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { message = "Strategy not found" });
            }
        })
        .WithName("AdminUpdateStrategy")
        .Produces<AdminStrategyResult>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/admin/strategies/{id} - Soft-delete strategy
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var deleted = await mediator.Send(
                new DeleteAdminStrategyCommand(id, session!.User!.Id), ct).ConfigureAwait(false);

            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { message = "Strategy not found" });
        })
        .WithName("AdminDeleteStrategy")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}

internal sealed record CreateAdminStrategyRequest(
    string Name,
    string? Description,
    string StepsJson);

internal sealed record UpdateAdminStrategyRequest(
    string Name,
    string? Description,
    string StepsJson);
