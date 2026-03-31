using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

internal static class GamePhaseTemplateEndpoints
{
    public static RouteGroupBuilder MapGamePhaseTemplateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/games/{gameId}/phase-templates", HandleGet)
            .RequireAuthorization()
            .Produces<IReadOnlyList<PhaseTemplateDto>>(200)
            .WithTags("PhaseTemplates")
            .WithSummary("Get phase templates for a game");

        group.MapPut("/games/{gameId}/phase-templates", HandleUpsert)
            .RequireAuthorization("RequireEditorOrAbove")
            .Produces<IReadOnlyList<PhaseTemplateDto>>(200)
            .Produces(400)
            .Produces(403)
            .WithTags("PhaseTemplates")
            .WithSummary("Upsert phase templates for a game (Editor/Admin/SuperAdmin only)");

        group.MapPost("/games/{gameId}/phase-templates/suggest", HandleSuggest)
            .RequireAuthorization("RequireEditorOrAbove")
            .Produces<IReadOnlyList<PhaseTemplateSuggestionDto>>(200)
            .Produces(404)
            .WithTags("PhaseTemplates")
            .WithSummary("AI-powered phase template suggestions from game rulebook");

        return group;
    }

    private static async Task<IResult> HandleGet(
        Guid gameId,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetPhaseTemplatesQuery(gameId), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpsert(
        Guid gameId,
        [FromBody] UpsertPhaseTemplatesRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var editorUserId = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? string.Empty;

        var command = new UpsertPhaseTemplatesCommand(gameId, editorUserId, request.Templates);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSuggest(
        Guid gameId,
        [FromServices] IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new SuggestPhaseTemplatesCommand(gameId), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

internal sealed record UpsertPhaseTemplatesRequest(IReadOnlyList<PhaseTemplateInput> Templates);
