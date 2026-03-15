using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Application.DTOs;
using Api.BoundedContexts.UserNotifications.Application.Queries;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Slack connection and team channel management.
/// Provides connection listing and team channel configuration overrides.
/// </summary>
internal static class AdminSlackEndpoints
{
    public static void MapAdminSlackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/slack")
            .WithTags("Admin - Slack")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapGet("/connections", HandleGetConnections)
            .WithName("GetAdminSlackConnections")
            .Produces<PaginatedSlackConnectionsResult>(200)
            .WithSummary("Get paginated list of Slack connections");

        group.MapGet("/team-channels", HandleGetTeamChannels)
            .WithName("GetSlackTeamChannels")
            .Produces<IReadOnlyList<SlackTeamChannelDto>>(200)
            .WithSummary("Get all Slack team channel configurations");

        group.MapPut("/team-channels/{id:guid}", HandleUpdateTeamChannel)
            .WithName("UpdateSlackTeamChannel")
            .Produces(204)
            .Produces(404)
            .WithSummary("Update a Slack team channel configuration");
    }

    private static async Task<IResult> HandleGetConnections(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetSlackConnectionsQuery(
            Page: page ?? 1,
            PageSize: pageSize ?? 20);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetTeamChannels(
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetSlackTeamChannelsQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpdateTeamChannel(
        Guid id,
        UpdateSlackTeamChannelRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateSlackTeamChannelCommand(
            id,
            request.ChannelName,
            request.WebhookUrl,
            request.NotificationTypes,
            request.IsEnabled,
            request.OverridesDefault);
        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
    }
}

internal record UpdateSlackTeamChannelRequest(
    string? ChannelName = null,
    string? WebhookUrl = null,
    string[]? NotificationTypes = null,
    bool? IsEnabled = null,
    bool? OverridesDefault = null);
