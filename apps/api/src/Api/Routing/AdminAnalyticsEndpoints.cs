using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Analytics;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin analytics endpoints for overview, chat, PDF, model performance, and MAU metrics.
/// </summary>
internal static class AdminAnalyticsEndpoints
{
    public static RouteGroupBuilder MapAdminAnalyticsEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/admin/analytics/overview
        group.MapGet("/admin/analytics/overview", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            var result = await mediator.Send(new GetOverviewAnalyticsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminDashboard_GetOverviewAnalytics")
        .WithTags("Admin", "Analytics")
        .Produces<OverviewAnalyticsDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // GET /api/v1/admin/analytics/chat
        group.MapGet("/admin/analytics/chat", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            var result = await mediator.Send(new GetChatAnalyticsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminDashboard_GetChatAnalytics")
        .WithTags("Admin", "Analytics")
        .Produces<ChatAnalyticsDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // GET /api/v1/admin/analytics/pdf
        group.MapGet("/admin/analytics/pdf", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            var result = await mediator.Send(new GetPdfAnalyticsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminDashboard_GetPdfAnalytics")
        .WithTags("Admin", "Analytics")
        .Produces<PdfAnalyticsDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // GET /api/v1/admin/analytics/model-performance
        group.MapGet("/admin/analytics/model-performance", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            var result = await mediator.Send(new GetModelPerformanceQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminDashboard_GetModelPerformance")
        .WithTags("Admin", "Analytics")
        .Produces<ModelPerformanceDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        // GET /api/v1/admin/analytics/mau
        group.MapGet("/admin/analytics/mau", async (HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;

            var result = await mediator.Send(new GetMauQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("AdminDashboard_GetMau")
        .WithTags("Admin", "Analytics")
        .Produces<MauDto>(200)
        .ProducesProblem(401)
        .ProducesProblem(403);

        return group;
    }
}
