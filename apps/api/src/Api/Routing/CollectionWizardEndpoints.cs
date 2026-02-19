using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Collection wizard endpoints for the AddGameSheet wizard flow.
/// Issue #4823: Backend Game Preview API - Unified Wizard Data Endpoint
/// Epic #4817: User Collection Wizard
/// </summary>
internal static class CollectionWizardEndpoints
{
    internal static RouteGroupBuilder MapCollectionWizardEndpoints(this RouteGroupBuilder group)
    {
        var wizardGroup = group.MapGroup("/wizard")
            .WithTags("Collection Wizard")
            .RequireAuthorization();

        wizardGroup.MapGet("/game-preview/{gameId:guid}", HandleGetGameWizardPreview)
            .WithName("GetGameWizardPreview")
            .WithSummary("Get combined game preview data for collection wizard")
            .WithDescription("Returns game metadata, PDF documents, library status, categories and mechanics in a single call. Used by the AddGameSheet wizard Steps 2-3.")
            .Produces<GameWizardPreviewDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// GET /api/v1/wizard/game-preview/{gameId}?source=catalog
    /// Returns combined preview data for the collection wizard.
    /// </summary>
    private static async Task<IResult> HandleGetGameWizardPreview(
        Guid gameId,
        HttpContext context,
        IMediator mediator,
        [Microsoft.AspNetCore.Mvc.FromQuery] string source = "catalog",
        CancellationToken ct = default)
    {
        var userId = context.User.GetUserId();

        var query = new GetGameWizardPreviewQuery(gameId, source, userId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }
}
