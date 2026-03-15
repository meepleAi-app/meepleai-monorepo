using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for notification queue monitoring and management.
/// Provides paginated queue views, dead letter management, metrics, and legacy migration tracking.
/// </summary>
internal static class AdminNotificationQueueEndpoints
{
    public static void MapAdminNotificationQueueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/notifications")
            .WithTags("Admin - Notification Queue")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapGet("/queue", HandleGetQueue)
            .WithName("GetNotificationQueue")
            .Produces<PaginatedNotificationQueueResult>(200)
            .WithSummary("Get paginated notification queue with optional channel/status filtering");

        group.MapGet("/dead-letter", HandleGetDeadLetter)
            .WithName("GetNotificationDeadLetter")
            .Produces<PaginatedNotificationQueueResult>(200)
            .WithSummary("Get paginated dead letter queue items");

        group.MapPost("/dead-letter/{id:guid}/retry", HandleRetryDeadLetter)
            .WithName("RetryDeadLetterNotification")
            .Produces(200)
            .Produces(404)
            .WithSummary("Retry a dead-lettered notification by resetting to Pending");

        group.MapGet("/metrics", HandleGetMetrics)
            .WithName("GetNotificationMetrics")
            .Produces<NotificationMetricsDto>(200)
            .WithSummary("Get notification queue metrics grouped by channel and status");

        group.MapGet("/legacy-queue/count", HandleGetLegacyCount)
            .WithName("GetLegacyQueueCount")
            .Produces<LegacyQueueCountResponse>(200)
            .WithSummary("Get count of remaining legacy EmailQueueItem rows (migration gate)");
    }

    private static async Task<IResult> HandleGetQueue(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? channel,
        [FromQuery] string? status,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetNotificationQueueQuery(
            Page: page ?? 1,
            PageSize: pageSize ?? 20,
            ChannelFilter: channel,
            StatusFilter: status);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetDeadLetter(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetDeadLetterQueueQuery(
            Page: page ?? 1,
            PageSize: pageSize ?? 20);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRetryDeadLetter(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new RetryDeadLetterCommand(id), ct).ConfigureAwait(false);
        return result ? Results.Ok(new { success = true }) : Results.NotFound();
    }

    private static async Task<IResult> HandleGetMetrics(
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetNotificationMetricsQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetLegacyCount(
        IMediator mediator,
        CancellationToken ct)
    {
        var count = await mediator.Send(new GetLegacyQueueCountQuery(), ct).ConfigureAwait(false);
        return Results.Ok(new LegacyQueueCountResponse(count));
    }
}

internal record LegacyQueueCountResponse(int Count);
