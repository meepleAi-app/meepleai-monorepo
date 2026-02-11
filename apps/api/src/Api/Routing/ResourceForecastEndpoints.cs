using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Resource Forecasting Simulator endpoints for admin users.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal static class ResourceForecastEndpoints
{
    public static RouteGroupBuilder MapResourceForecastEndpoints(this RouteGroupBuilder group)
    {
        var forecastGroup = group.MapGroup("/admin/resource-forecast")
            .WithTags("Admin - Resource Forecast");

        // POST /api/v1/admin/resource-forecast/estimate - Compute 12-month projections
        forecastGroup.MapPost("/estimate", HandleEstimateForecast)
            .WithName("EstimateResourceForecast")
            .WithSummary("Compute 12-month resource projections based on current metrics and growth parameters")
            .Produces<ResourceForecastEstimationResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // POST /api/v1/admin/resource-forecast/scenarios - Save a forecast scenario
        forecastGroup.MapPost("/scenarios", HandleSaveScenario)
            .WithName("SaveResourceForecast")
            .WithSummary("Save a resource forecast scenario for later reference")
            .Produces<SaveForecastResponseDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/resource-forecast/scenarios - List saved scenarios
        forecastGroup.MapGet("/scenarios", HandleGetScenarios)
            .WithName("GetResourceForecasts")
            .WithSummary("Get saved resource forecast scenarios for the current user")
            .Produces<ResourceForecastsResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // DELETE /api/v1/admin/resource-forecast/scenarios/{id} - Delete a scenario
        forecastGroup.MapDelete("/scenarios/{id:guid}", HandleDeleteScenario)
            .WithName("DeleteResourceForecast")
            .WithSummary("Delete a saved resource forecast scenario")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> HandleEstimateForecast(
        EstimateResourceForecastRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new EstimateResourceForecastQuery(
            request.GrowthPattern,
            request.MonthlyGrowthRate,
            request.CurrentUsers,
            request.CurrentDbSizeGb,
            request.CurrentDailyTokens,
            request.CurrentCacheMb,
            request.CurrentVectorEntries,
            request.DbPerUserMb,
            request.TokensPerUserPerDay,
            request.CachePerUserMb,
            request.VectorsPerUser);

        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSaveScenario(
        SaveResourceForecastRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var userId = session!.User!.Id;

        var command = new SaveResourceForecastCommand(
            request.Name,
            request.GrowthPattern,
            request.MonthlyGrowthRate,
            request.CurrentUsers,
            request.CurrentDbSizeGb,
            request.CurrentDailyTokens,
            request.CurrentCacheMb,
            request.CurrentVectorEntries,
            request.DbPerUserMb,
            request.TokensPerUserPerDay,
            request.CachePerUserMb,
            request.VectorsPerUser,
            request.ProjectionsJson,
            request.RecommendationsJson,
            request.ProjectedMonthlyCost,
            userId);

        var forecastId = await mediator.Send(command, cancellationToken).ConfigureAwait(false);

        return Results.Created(
            $"/api/v1/admin/resource-forecast/scenarios/{forecastId}",
            new SaveForecastResponseDto(forecastId));
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

        var query = new GetResourceForecastsQuery(
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

        var command = new DeleteResourceForecastCommand(id);
        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }
}

/// <summary>
/// Request DTO for resource forecast estimation
/// </summary>
internal sealed record EstimateResourceForecastRequest(
    string GrowthPattern,
    decimal MonthlyGrowthRate,
    int CurrentUsers,
    decimal CurrentDbSizeGb,
    long CurrentDailyTokens,
    decimal CurrentCacheMb,
    long CurrentVectorEntries,
    decimal DbPerUserMb,
    int TokensPerUserPerDay,
    decimal CachePerUserMb,
    int VectorsPerUser);

/// <summary>
/// Request DTO for saving a resource forecast scenario
/// </summary>
internal sealed record SaveResourceForecastRequest(
    string Name,
    string GrowthPattern,
    decimal MonthlyGrowthRate,
    int CurrentUsers,
    decimal CurrentDbSizeGb,
    long CurrentDailyTokens,
    decimal CurrentCacheMb,
    long CurrentVectorEntries,
    decimal DbPerUserMb,
    int TokensPerUserPerDay,
    decimal CachePerUserMb,
    int VectorsPerUser,
    string ProjectionsJson,
    string? RecommendationsJson,
    decimal ProjectedMonthlyCost);

/// <summary>
/// Response DTO after saving a forecast
/// </summary>
internal sealed record SaveForecastResponseDto(Guid Id);
