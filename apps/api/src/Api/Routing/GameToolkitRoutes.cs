using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// GameToolkit API routes (Issue #4753, #4759).
/// Handles game toolkit CRUD, tool management, and template configuration.
/// </summary>
internal static class GameToolkitRoutes
{
    public static RouteGroupBuilder MapGameToolkitEndpoints(this RouteGroupBuilder group)
    {
        var toolkits = group.MapGroup("/game-toolkits")
            .WithTags("GameToolkits")
            .RequireAuthorization();

        // ---- Queries ----

        toolkits.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetToolkitQuery(id)).ConfigureAwait(false);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetGameToolkit")
        .WithSummary("Get a game toolkit by ID")
        .Produces<GameToolkitDto>(200)
        .Produces(404);

        toolkits.MapGet("/by-game/{gameId:guid}", async (Guid gameId, IMediator m) =>
        {
            var result = await m.Send(new GetToolkitsByGameQuery(gameId)).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetToolkitsByGame")
        .WithSummary("Get all toolkits for a game")
        .Produces<IReadOnlyList<GameToolkitDto>>(200);

        toolkits.MapGet("/published", async (IMediator m) =>
        {
            var result = await m.Send(new GetPublishedToolkitsQuery()).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetPublishedToolkits")
        .WithSummary("Get all published toolkits")
        .Produces<IReadOnlyList<GameToolkitDto>>(200);

        // ---- Commands ----

        toolkits.MapPost("/", async (CreateToolkitRequest request, HttpContext ctx, IMediator m) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId == Guid.Empty)
                return Results.Unauthorized();
            var command = new CreateToolkitCommand(request.GameId, request.Name, userId);
            var result = await m.Send(command).ConfigureAwait(false);
            return Results.Created($"/api/v1/game-toolkits/{result.Id}", result);
        })
        .WithName("CreateGameToolkit")
        .WithSummary("Create a new game toolkit")
        .Produces<GameToolkitDto>(201);

        toolkits.MapPut("/{id:guid}", async (Guid id, UpdateToolkitRequest request, IMediator m) =>
        {
            var command = new UpdateToolkitCommand(id, request.Name);
            var result = await m.Send(command).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("UpdateGameToolkit")
        .WithSummary("Update toolkit details")
        .Produces<GameToolkitDto>(200);

        toolkits.MapPost("/{id:guid}/publish", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new PublishToolkitCommand(id)).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("PublishGameToolkit")
        .WithSummary("Publish a toolkit")
        .Produces<GameToolkitDto>(200);

        // ---- Dice Tools ----

        toolkits.MapPost("/{id:guid}/dice-tools", async (Guid id, AddDiceToolRequest request, IMediator m) =>
        {
            var command = new AddDiceToolCommand(
                id, request.Name, request.DiceType, request.Quantity,
                request.CustomFaces, request.IsInteractive, request.Color);
            var result = await m.Send(command).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AddDiceTool")
        .WithSummary("Add a dice tool to the toolkit")
        .Produces<GameToolkitDto>(200);

        toolkits.MapDelete("/{id:guid}/dice-tools/{toolName}", async (Guid id, string toolName, IMediator m) =>
        {
            var result = await m.Send(new RemoveDiceToolCommand(id, toolName)).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("RemoveDiceTool")
        .WithSummary("Remove a dice tool from the toolkit")
        .Produces<GameToolkitDto>(200);

        // ---- Card Tools ----

        toolkits.MapPost("/{id:guid}/card-tools", async (Guid id, AddCardToolRequest request, IMediator m) =>
        {
            var command = new AddCardToolCommand(
                id, request.Name, request.DeckType, request.CardCount, request.Shuffleable,
                request.DefaultZone, request.DefaultOrientation, request.CardEntries,
                request.AllowDraw, request.AllowDiscard, request.AllowPeek, request.AllowReturnToDeck);
            var result = await m.Send(command).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AddCardTool")
        .WithSummary("Add a card tool to the toolkit")
        .Produces<GameToolkitDto>(200);

        toolkits.MapDelete("/{id:guid}/card-tools/{toolName}", async (Guid id, string toolName, IMediator m) =>
        {
            var result = await m.Send(new RemoveCardToolCommand(id, toolName)).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("RemoveCardTool")
        .WithSummary("Remove a card tool from the toolkit")
        .Produces<GameToolkitDto>(200);

        // ---- Timer Tools ----

        toolkits.MapPost("/{id:guid}/timer-tools", async (Guid id, AddTimerToolRequest request, IMediator m) =>
        {
            var command = new AddTimerToolCommand(
                id, request.Name, request.DurationSeconds, request.TimerType,
                request.AutoStart, request.Color, request.IsPerPlayer, request.WarningThresholdSeconds);
            var result = await m.Send(command).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AddTimerTool")
        .WithSummary("Add a timer tool to the toolkit")
        .Produces<GameToolkitDto>(200);

        toolkits.MapDelete("/{id:guid}/timer-tools/{toolName}", async (Guid id, string toolName, IMediator m) =>
        {
            var result = await m.Send(new RemoveTimerToolCommand(id, toolName)).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("RemoveTimerTool")
        .WithSummary("Remove a timer tool from the toolkit")
        .Produces<GameToolkitDto>(200);

        // ---- Counter Tools ----

        toolkits.MapPost("/{id:guid}/counter-tools", async (Guid id, AddCounterToolRequest request, IMediator m) =>
        {
            var command = new AddCounterToolCommand(
                id, request.Name, request.MinValue, request.MaxValue,
                request.DefaultValue, request.IsPerPlayer, request.Icon, request.Color);
            var result = await m.Send(command).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AddCounterTool")
        .WithSummary("Add a counter tool to the toolkit")
        .Produces<GameToolkitDto>(200);

        toolkits.MapDelete("/{id:guid}/counter-tools/{toolName}", async (Guid id, string toolName, IMediator m) =>
        {
            var result = await m.Send(new RemoveCounterToolCommand(id, toolName)).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("RemoveCounterTool")
        .WithSummary("Remove a counter tool from the toolkit")
        .Produces<GameToolkitDto>(200);

        // ---- Templates ----

        toolkits.MapPut("/{id:guid}/scoring-template", async (Guid id, SetScoringTemplateRequest request, IMediator m) =>
        {
            var command = new SetScoringTemplateCommand(id, request.Dimensions, request.DefaultUnit, request.ScoreType);
            var result = await m.Send(command).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("SetScoringTemplate")
        .WithSummary("Set or update the scoring template")
        .Produces<GameToolkitDto>(200);

        toolkits.MapPut("/{id:guid}/turn-template", async (Guid id, SetTurnTemplateRequest request, IMediator m) =>
        {
            var command = new SetTurnTemplateCommand(id, request.TurnOrderType, request.Phases);
            var result = await m.Send(command).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("SetTurnTemplate")
        .WithSummary("Set or update the turn template")
        .Produces<GameToolkitDto>(200);

        toolkits.MapPut("/{id:guid}/state-template", async (Guid id, SetStateTemplateRequest request, IMediator m) =>
        {
            var command = new SetStateTemplateCommand(id, request.Name, request.Category, request.SchemaJson, request.Description);
            var result = await m.Send(command).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("SetStateTemplate")
        .WithSummary("Set or update the state template")
        .Produces<GameToolkitDto>(200);

        toolkits.MapDelete("/{id:guid}/state-template", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new ClearStateTemplateCommand(id)).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("ClearStateTemplate")
        .WithSummary("Clear the state template")
        .Produces<GameToolkitDto>(200);

        return toolkits;
    }
}
