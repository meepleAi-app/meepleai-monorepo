using Api.BoundedContexts.Administration.Application.Queries.Budget;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Budget overview for the admin (OpenRouter balance and cost logs).
/// Issue #4212: the per-user credit budget route (GET /budget/user/{id}) is retired with the
/// token-tier system it read.
/// </summary>
internal static class BudgetEndpoints
{
    public static RouteGroupBuilder MapBudgetEndpoints(this RouteGroupBuilder group)
    {
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
