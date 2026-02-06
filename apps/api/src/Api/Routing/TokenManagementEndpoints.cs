using Api.BoundedContexts.Administration.Application.Commands.TokenManagement;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.TokenManagement;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Token Management endpoints for enterprise resource monitoring (Issue #3692)
/// </summary>
internal static class TokenManagementEndpoints
{
    public static RouteGroupBuilder MapTokenManagementEndpoints(this RouteGroupBuilder group)
    {
        var tokensGroup = group.MapGroup("/admin/resources/tokens")
            .WithTags("Admin - Token Management");

        // GET /api/v1/admin/resources/tokens - Get token balance
        tokensGroup.MapGet("/", HandleGetTokenBalance)
            .WithName("GetTokenBalance")
            .WithSummary("Get current token balance and usage summary")
            .Produces<TokenBalanceDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/resources/tokens/consumption - Get consumption trend
        tokensGroup.MapGet("/consumption", HandleGetTokenConsumption)
            .WithName("GetTokenConsumption")
            .WithSummary("Get token consumption trend data")
            .Produces<TokenConsumptionDataDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/resources/tokens/tiers - Get tier usage
        tokensGroup.MapGet("/tiers", HandleGetTokenTierUsage)
            .WithName("GetTokenTierUsage")
            .WithSummary("Get token usage breakdown per tier")
            .Produces<TierUsageListDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // GET /api/v1/admin/resources/tokens/top-consumers - Get top consumers
        tokensGroup.MapGet("/top-consumers", HandleGetTopConsumers)
            .WithName("GetTopConsumers")
            .WithSummary("Get top token consumers")
            .Produces<TopConsumersListDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // PUT /api/v1/admin/resources/tokens/tiers/{tier} - Update tier limits
        tokensGroup.MapPut("/tiers/{tier}", HandleUpdateTierLimits)
            .WithName("UpdateTierLimits")
            .WithSummary("Update tier token limits")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // POST /api/v1/admin/resources/tokens/add-credits - Add credits
        tokensGroup.MapPost("/add-credits", HandleAddTokenCredits)
            .WithName("AddTokenCredits")
            .WithSummary("Add credits to token balance")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<IResult> HandleGetTokenBalance(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetTokenBalanceQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetTokenConsumption(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        int days = 30)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetTokenConsumptionQuery(days);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetTokenTierUsage(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetTokenTierUsageQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetTopConsumers(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct,
        int limit = 10)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetTopConsumersQuery(limit);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateTierLimits(
        string tier,
        UpdateTierLimitsRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new UpdateTierLimitsCommand(tier, request.TokensPerMonth);
        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleAddTokenCredits(
        AddCreditsRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        logger.LogInformation(
            "Admin {AdminId} adding token credits: {Amount} {Currency}",
            session!.User!.Id,
            request.Amount,
            request.Currency);

        var command = new AddTokenCreditsCommand(request.Amount, request.Currency, request.Note);
        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
}
