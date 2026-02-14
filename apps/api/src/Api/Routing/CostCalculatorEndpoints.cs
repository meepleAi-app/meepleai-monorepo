using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Agent Cost Calculator endpoints for admin users.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal static class CostCalculatorEndpoints
{
    public static RouteGroupBuilder MapCostCalculatorEndpoints(this RouteGroupBuilder group)
    {
        var calcGroup = group.MapGroup("/admin/cost-calculator")
            .WithTags("Admin - Cost Calculator");

        // POST /api/v1/admin/cost-calculator/estimate - Estimate agent cost
        calcGroup.MapPost("/estimate", HandleEstimateCost)
            .WithName("EstimateAgentCost")
            .WithSummary("Estimate agent costs based on strategy, model, and usage parameters")
            .Produces<AgentCostEstimationResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/cost-calculator/scenarios - Save a cost scenario
        calcGroup.MapPost("/scenarios", HandleSaveScenario)
            .WithName("SaveCostScenario")
            .WithSummary("Save a cost estimation scenario for later reference")
            .Produces<SaveScenarioResponseDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/cost-calculator/scenarios - List saved scenarios
        calcGroup.MapGet("/scenarios", HandleGetScenarios)
            .WithName("GetCostScenarios")
            .WithSummary("Get saved cost scenarios for the current user")
            .Produces<CostScenariosResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/admin/cost-calculator/scenarios/{id} - Delete a scenario
        calcGroup.MapDelete("/scenarios/{id:guid}", HandleDeleteScenario)
            .WithName("DeleteCostScenario")
            .WithSummary("Delete a saved cost scenario")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> HandleEstimateCost(
        EstimateAgentCostRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new EstimateAgentCostQuery(
            request.Strategy,
            request.ModelId,
            request.MessagesPerDay,
            request.ActiveUsers,
            request.AvgTokensPerRequest);

        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSaveScenario(
        SaveCostScenarioRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        var command = new SaveCostScenarioCommand(
            request.Name,
            request.Strategy,
            request.ModelId,
            request.MessagesPerDay,
            request.ActiveUsers,
            request.AvgTokensPerRequest,
            request.CostPerRequest,
            request.DailyProjection,
            request.MonthlyProjection,
            request.Warnings,
            userId);

        var scenarioId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Created(
            $"/api/v1/admin/cost-calculator/scenarios/{scenarioId}",
            new SaveScenarioResponseDto(scenarioId));
    }

    private static async Task<IResult> HandleGetScenarios(
        HttpContext context,
        IMediator mediator,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        var query = new GetCostScenariosQuery(
            userId,
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 100));

        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeleteScenario(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new DeleteCostScenarioCommand(id);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}

/// <summary>
/// Request DTO for cost estimation
/// </summary>
internal sealed record EstimateAgentCostRequest(
    string Strategy,
    string ModelId,
    int MessagesPerDay,
    int ActiveUsers,
    int AvgTokensPerRequest);

/// <summary>
/// Request DTO for saving a cost scenario
/// </summary>
internal sealed record SaveCostScenarioRequest(
    string Name,
    string Strategy,
    string ModelId,
    int MessagesPerDay,
    int ActiveUsers,
    int AvgTokensPerRequest,
    decimal CostPerRequest,
    decimal DailyProjection,
    decimal MonthlyProjection,
    List<string>? Warnings);

/// <summary>
/// Response DTO after saving a scenario
/// </summary>
internal sealed record SaveScenarioResponseDto(Guid Id);
