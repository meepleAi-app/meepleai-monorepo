using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Chat Analytics endpoints for admin dashboard.
/// Issue #3714: Chat thread metrics - total threads, messages, agent types,
/// daily trends, unique users.
/// </summary>
internal static class ChatAnalyticsEndpoints
{
    internal static void MapChatAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/chat-analytics")
            .WithTags("Admin - Chat Analytics")
            .AddEndpointFilter<Filters.RequireAdminSessionFilter>();

        group.MapGet("/", HandleGetChatAnalytics)
            .WithName("GetChatAnalytics")
            .WithSummary("Get chat analytics (Admin only)")
            .WithDescription(
                "Returns aggregated chat analytics including total threads, " +
                "active/closed counts, message totals, average messages per thread, " +
                "threads by agent type, unique users, and daily trends. " +
                "Supports time range filtering. (Issue #3714)")
            .Produces<ChatAnalyticsDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> HandleGetChatAnalytics(
        HttpContext context,
        IMediator mediator,
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var query = new GetChatAnalyticsQuery(
            TimeRangeDays: Math.Clamp(days, 1, 365));

        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
