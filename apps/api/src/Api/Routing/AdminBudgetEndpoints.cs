using Api.BoundedContexts.BusinessSimulations.Application.Commands.AppBudget;
using Api.BoundedContexts.BusinessSimulations.Application.Queries.AppBudget;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for the singleton AppBudget configuration (Issue #1838 SP5 F4-C5).
///
/// <list type="bullet">
///   <item><c>GET /api/v1/admin/budget</c> — current limit + computed spend KPIs + xmin</item>
///   <item><c>PUT /api/v1/admin/budget</c> — upsert limit + alert/critical thresholds (concurrency-checked)</item>
/// </list>
///
/// <para>All endpoints follow the CQRS rule: routing → IMediator.Send only.
/// Auth is enforced per-endpoint via <c>RequireAdminSession()</c> (Admin/SuperAdmin role check).
/// Path-wise: invoked on the <c>v1Api</c> group which already applies the
/// <c>/api/v1</c> prefix, so the inner MapGroup uses the relative segment
/// <c>/admin/budget</c> only — hardcoding <c>/api/v1</c> here would produce a
/// double prefix and 404 all FE calls (lesson from PR #1840).</para>
///
/// <para>Note on path overlap with <c>BudgetEndpoints</c>: that file registers
/// <c>GET /admin/budget/overview</c> (legacy credit-tracking display) which
/// resolves independently of <c>GET /admin/budget</c> / <c>PUT /admin/budget</c>
/// added here. No shadowing.</para>
/// </summary>
internal static class AdminBudgetEndpoints
{
    public static void MapAdminBudgetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/budget")
            .WithTags("Admin", "Budget");

        // GET /api/v1/admin/budget
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
            {
                var budget = await mediator.Send(new GetAppBudgetQuery(), ct).ConfigureAwait(false);
                return budget is null
                    ? Results.Ok((AppBudgetDto?)null)
                    : Results.Ok(budget);
            })
            .RequireAdminSession()
            .WithName("AdminBudget_GetCurrent")
            .WithSummary("Get the singleton AppBudget (limit + thresholds + computed spend KPIs)")
            .WithOpenApi();

        // PUT /api/v1/admin/budget
        group.MapPut("/", async (
                [FromBody] UpsertAppBudgetRequest request,
                HttpContext context,
                IMediator mediator,
                CancellationToken ct) =>
            {
                ArgumentNullException.ThrowIfNull(request);
                var userId = context.User.FindFirst("userId")?.Value ?? "system";

                var command = new UpsertAppBudgetCommand(
                    MonthlyLimitAmount: request.MonthlyLimitAmount,
                    MonthlyLimitCurrency: request.MonthlyLimitCurrency,
                    AlertThresholdPct: request.AlertThresholdPct,
                    CriticalThresholdPct: request.CriticalThresholdPct,
                    Xmin: request.Xmin,
                    UpdatedBy: userId);

                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            })
            .RequireAdminSession()
            .WithName("AdminBudget_Upsert")
            .WithSummary("Create or update the singleton AppBudget (with optimistic concurrency)")
            .WithOpenApi();
    }
}

/// <summary>
/// Request body for <c>PUT /api/v1/admin/budget</c>. Xmin is omitted on
/// first-time creation; required for in-place updates.
/// </summary>
internal sealed record UpsertAppBudgetRequest(
    decimal MonthlyLimitAmount,
    string MonthlyLimitCurrency,
    int AlertThresholdPct,
    int CriticalThresholdPct,
    uint? Xmin);
