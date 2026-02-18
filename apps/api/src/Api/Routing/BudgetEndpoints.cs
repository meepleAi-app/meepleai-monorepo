using Api.BoundedContexts.Administration.Application.Queries.Budget;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Budget management endpoints for credit tracking and admin overview
/// </summary>
internal static class BudgetEndpoints
{
    public static RouteGroupBuilder MapBudgetEndpoints(this RouteGroupBuilder group)
    {
        // User budget endpoints
        var userGroup = group.MapGroup("/budget/user")
            .WithTags("Budget - User");

        userGroup.MapGet("/{userId:guid}", HandleGetUserBudget)
            .WithName("GetUserBudget")
            .WithSummary("Get user's credit budget status")
            .WithDescription("Returns credit balance, daily/weekly limits, and reset timers. Self-access only.")
            .Produces<UserBudgetStatus>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // Admin budget endpoints
        var adminGroup = group.MapGroup("/admin/budget")
            .WithTags("Admin - Budget");

        adminGroup.MapGet("/overview", HandleGetAdminBudgetOverview)
            .WithName("GetAdminBudgetOverview")
            .WithSummary("Get admin budget overview")
            .WithDescription("Returns OpenRouter balance (€) and app daily/weekly budget ($) with usage percentages. Admin-only.")
            .Produces<AdminBudgetOverview>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        return group;
    }

    private static async Task<IResult> HandleGetUserBudget(
        Guid userId,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Require authenticated session
        var (authenticated, session, error) = context.TryGetActiveSession();
        if (!authenticated) return error!;

        // Self-access only: users can only view their own budget
        if (session!.User!.Id != userId)
        {
            return Results.Forbid();
        }

        var query = new GetUserBudgetQuery(userId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetAdminBudgetOverview(
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        // Admin-only access
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetAdminBudgetOverviewQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
