using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

public static class AiModelEndpoints
{
    public static RouteGroupBuilder MapAiModelEndpoints(this RouteGroupBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/ai-models")
            .WithTags("Admin", "AI Models")
            .RequireAuthorization();

        group.MapGet("", HandleGetAllAiModels)
            .WithName("GetAllAiModels")
            .Produces<IReadOnlyList<object>>(200);

        group.MapGet("{id:guid}", HandleGetAiModelById)
            .WithName("GetAiModelById")
            .Produces<object>(200)
            .Produces(404);

        group.MapPost("", HandleCreateAiModel)
            .WithName("CreateAiModel")
            .Produces<object>(201)
            .Produces(400);

        group.MapPut("{id:guid}", HandleUpdateAiModel)
            .WithName("UpdateAiModel")
            .Produces<object>(200)
            .Produces(404);

        group.MapDelete("{id:guid}", HandleDeleteAiModel)
            .WithName("DeleteAiModel")
            .Produces(204)
            .Produces(404);

        group.MapPatch("{id:guid}/priority", HandleUpdatePriority)
            .WithName("UpdateAiModelPriority")
            .Produces(204)
            .Produces(404);

        group.MapPatch("{id:guid}/toggle", HandleToggleActive)
            .WithName("ToggleAiModelActive")
            .Produces(204)
            .Produces(404);

        return group;
    }

    private static async Task<IResult> HandleGetAllAiModels(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        context.RequireAdminSession();
        var models = await mediator.Send(new GetAllAiModelsQuery(), ct).ConfigureAwait(false);
        return Results.Ok(models);
    }

    private static async Task<IResult> HandleUpdatePriority(
        Guid id,
        UpdatePriorityRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        context.RequireAdminSession();
        await mediator.Send(new UpdateAiModelPriorityCommand(id, request.NewPriority), ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleToggleActive(
        Guid id,
        ToggleActiveRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        context.RequireAdminSession();
        await mediator.Send(new ToggleAiModelActiveCommand(id, request.IsActive), ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleGetAiModelById(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        context.RequireAdminSession();
        var model = await mediator.Send(new GetAiModelByIdQuery(id), ct).ConfigureAwait(false);
        return Results.Ok(model);
    }

    private static async Task<IResult> HandleCreateAiModel(
        CreateAiModelRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        context.RequireAdminSession();
        var command = new CreateAiModelConfigCommand(
            request.ModelId,
            request.DisplayName,
            request.Provider,
            request.Priority,
            request.Settings,
            request.Pricing);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/admin/ai-models/{result.Id}", result);
    }

    private static async Task<IResult> HandleUpdateAiModel(
        Guid id,
        UpdateAiModelRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        context.RequireAdminSession();
        var command = new UpdateAiModelConfigCommand(
            id,
            request.DisplayName,
            request.Priority,
            request.IsActive,
            request.IsPrimary,
            request.Settings,
            request.Pricing);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeleteAiModel(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        context.RequireAdminSession();
        await mediator.Send(new DeleteAiModelConfigCommand(id), ct).ConfigureAwait(false);
        return Results.NoContent();
    }
}

internal sealed record CreateAiModelRequest(
    string ModelId,
    string DisplayName,
    string Provider,
    int Priority,
    ModelSettingsDto? Settings,
    ModelPricingDto? Pricing);

internal sealed record UpdateAiModelRequest(
    string? DisplayName,
    int? Priority,
    bool? IsActive,
    bool? IsPrimary,
    ModelSettingsDto? Settings,
    ModelPricingDto? Pricing);

internal sealed record UpdatePriorityRequest(int NewPriority);
internal sealed record ToggleActiveRequest(bool IsActive);
