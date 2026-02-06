using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Commands.ProposalMigrations;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.ProposalMigrations;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Proposal migration endpoints for post-approval user choices.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
internal static class ProposalMigrationEndpoints
{
    public static RouteGroupBuilder MapProposalMigrationEndpoints(this RouteGroupBuilder group)
    {
        MapGetPendingMigrationsEndpoint(group);
        MapHandleMigrationChoiceEndpoint(group);

        return group;
    }

    /// <summary>
    /// GET /api/v1/migrations/pending - Get pending migrations for authenticated user
    /// </summary>
    private static void MapGetPendingMigrationsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/migrations/pending", async (
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

            var query = new GetPendingMigrationsQuery(UserId: userId);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetPendingMigrations")
        .WithTags("ProposalMigrations")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Get pending migrations";
            operation.Description = "Retrieve all pending migration choices for approved private game proposals. " +
                "Users can choose to link their library entry to the new SharedGame or keep the PrivateGame separate.";
            return operation;
        })
        .Produces<List<ProposalMigrationDto>>()
        .Produces(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// POST /api/v1/migrations/{id}/choose - Handle migration choice
    /// Rate limit: 2 requests per minute
    /// </summary>
    private static void MapHandleMigrationChoiceEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/migrations/{id:guid}/choose", async (
            Guid id,
            [FromBody] HandleMigrationChoiceRequest request,
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

            var command = new HandleMigrationChoiceCommand(
                MigrationId: id,
                UserId: userId,
                Choice: request.Choice
            );

            await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("HandleMigrationChoice")
        .WithTags("ProposalMigrations")
        .WithOpenApi(operation =>
        {
            operation.Summary = "Choose migration action";
            operation.Description = "Choose how to handle your library entry after a private game proposal is approved. " +
                "LinkToCatalog: migrates your library entry to the new SharedGame and deletes your PrivateGame. " +
                "KeepPrivate: keeps your PrivateGame separate from the new SharedGame.";
            return operation;
        })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
        .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
        .Produces<ProblemDetails>(StatusCodes.Status409Conflict);
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

/// <summary>
/// Request DTO for handling a migration choice.
/// </summary>
internal record HandleMigrationChoiceRequest(
    PostApprovalMigrationChoice Choice
);
