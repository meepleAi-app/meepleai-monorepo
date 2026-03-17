using Api.BoundedContexts.GameToolbox.Application.Commands;
using Api.BoundedContexts.GameToolbox.Application.DTOs;
using Api.BoundedContexts.GameToolbox.Application.Queries;
using MediatR;

namespace Api.Routing;

/// <summary>
/// GameToolbox API routes — Epic #412.
/// </summary>
internal static class GameToolboxRoutes
{
    public static RouteGroupBuilder MapGameToolboxEndpoints(this RouteGroupBuilder group)
    {
        var toolboxes = group.MapGroup("/toolboxes")
            .WithTags("Toolboxes")
            .RequireAuthorization();

        // Queries
        toolboxes.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetToolboxQuery(id)).ConfigureAwait(false);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        }).WithName("GetToolbox").Produces<ToolboxDto>(200).Produces(404);

        toolboxes.MapGet("/by-game/{gameId:guid}", async (Guid gameId, IMediator m) =>
        {
            var result = await m.Send(new GetToolboxByGameQuery(gameId)).ConfigureAwait(false);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        }).WithName("GetToolboxByGame").Produces<ToolboxDto>(200).Produces(404);

        toolboxes.MapGet("/available-tools", async (IMediator m) =>
        {
            var result = await m.Send(new GetAvailableToolsQuery()).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("GetAvailableTools").Produces<List<AvailableToolDto>>(200);

        // Toolbox CRUD
        toolboxes.MapPost("/", async (CreateToolboxRequest req, IMediator m) =>
        {
            var result = await m.Send(new CreateToolboxCommand(req.Name, req.GameId, req.Mode)).ConfigureAwait(false);
            return Results.Created($"/api/v1/toolboxes/{result.Id}", result);
        }).WithName("CreateToolbox").Produces<ToolboxDto>(201);

        toolboxes.MapPut("/{id:guid}/mode", async (Guid id, string mode, IMediator m) =>
        {
            var result = await m.Send(new UpdateToolboxModeCommand(id, mode)).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("UpdateToolboxMode").Produces<ToolboxDto>(200);

        // Tool management
        toolboxes.MapPost("/{id:guid}/tools", async (Guid id, AddToolRequest req, IMediator m) =>
        {
            var result = await m.Send(new AddToolToToolboxCommand(id, req.Type, req.Config)).ConfigureAwait(false);
            return Results.Created($"/api/v1/toolboxes/{id}/tools/{result.Id}", result);
        }).WithName("AddToolToToolbox").Produces<ToolboxToolDto>(201);

        toolboxes.MapDelete("/{id:guid}/tools/{toolId:guid}", async (Guid id, Guid toolId, IMediator m) =>
        {
            await m.Send(new RemoveToolFromToolboxCommand(id, toolId)).ConfigureAwait(false);
            return Results.NoContent();
        }).WithName("RemoveToolFromToolbox").Produces(204);

        toolboxes.MapPut("/{id:guid}/tools/reorder", async (Guid id, ReorderRequest req, IMediator m) =>
        {
            await m.Send(new ReorderToolsCommand(id, req.OrderedIds)).ConfigureAwait(false);
            return Results.NoContent();
        }).WithName("ReorderTools").Produces(204);

        // Shared context
        toolboxes.MapPut("/{id:guid}/shared-context", async (Guid id, UpdateSharedContextRequest req, IMediator m) =>
        {
            var result = await m.Send(new UpdateSharedContextCommand(
                id, req.Players, req.CurrentPlayerIndex, req.CurrentRound,
                req.CustomProperties ?? new Dictionary<string, string>(StringComparer.Ordinal)
            )).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("UpdateSharedContext").Produces<SharedContextDto>(200);

        // Card deck
        toolboxes.MapPost("/{id:guid}/card-decks", async (Guid id, CreateCardDeckRequest req, IMediator m) =>
        {
            var result = await m.Send(new CreateCardDeckCommand(id, req.Name, req.DeckType, req.CustomCards)).ConfigureAwait(false);
            return Results.Created($"/api/v1/toolboxes/{id}/tools/{result.Id}", result);
        }).WithName("CreateCardDeck").Produces<ToolboxToolDto>(201);

        toolboxes.MapPost("/{id:guid}/card-decks/{deckId:guid}/shuffle", async (Guid id, Guid deckId, IMediator m) =>
        {
            await m.Send(new ShuffleCardDeckCommand(id, deckId)).ConfigureAwait(false);
            return Results.NoContent();
        }).WithName("ShuffleCardDeck").Produces(204);

        toolboxes.MapPost("/{id:guid}/card-decks/{deckId:guid}/draw", async (Guid id, Guid deckId, int count, IMediator m) =>
        {
            var result = await m.Send(new DrawCardsCommand(id, deckId, count)).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("DrawCards").Produces<CardDrawResultDto>(200);

        toolboxes.MapPost("/{id:guid}/card-decks/{deckId:guid}/reset", async (Guid id, Guid deckId, IMediator m) =>
        {
            await m.Send(new ResetCardDeckCommand(id, deckId)).ConfigureAwait(false);
            return Results.NoContent();
        }).WithName("ResetCardDeck").Produces(204);

        // Phases
        toolboxes.MapPost("/{id:guid}/phases", async (Guid id, AddPhaseRequest req, IMediator m) =>
        {
            var result = await m.Send(new AddPhaseCommand(id, req.Name, req.ActiveToolIds)).ConfigureAwait(false);
            return Results.Created($"/api/v1/toolboxes/{id}/phases/{result.Id}", result);
        }).WithName("AddPhase").Produces<PhaseDto>(201);

        toolboxes.MapDelete("/{id:guid}/phases/{phaseId:guid}", async (Guid id, Guid phaseId, IMediator m) =>
        {
            await m.Send(new RemovePhaseCommand(id, phaseId)).ConfigureAwait(false);
            return Results.NoContent();
        }).WithName("RemovePhase").Produces(204);

        toolboxes.MapPut("/{id:guid}/phases/reorder", async (Guid id, ReorderRequest req, IMediator m) =>
        {
            await m.Send(new ReorderPhasesCommand(id, req.OrderedIds)).ConfigureAwait(false);
            return Results.NoContent();
        }).WithName("ReorderPhases").Produces(204);

        toolboxes.MapPost("/{id:guid}/phases/advance", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new AdvancePhaseCommand(id)).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("AdvancePhase").Produces<PhaseDto>(200);

        // Templates
        var templates = group.MapGroup("/toolbox-templates")
            .WithTags("ToolboxTemplates")
            .RequireAuthorization();

        templates.MapGet("/", async (Guid? gameId, IMediator m) =>
        {
            var result = await m.Send(new GetToolboxTemplatesQuery(gameId)).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("GetToolboxTemplates").Produces<List<ToolboxTemplateDto>>(200);

        templates.MapPost("/{templateId:guid}/apply", async (Guid templateId, ApplyToolboxTemplateRequest req, IMediator m) =>
        {
            var result = await m.Send(new ApplyToolboxTemplateCommand(templateId, req.GameId)).ConfigureAwait(false);
            return Results.Created($"/api/v1/toolboxes/{result.Id}", result);
        }).WithName("ApplyToolboxTemplate").Produces<ToolboxDto>(201);

        return group;
    }
}
