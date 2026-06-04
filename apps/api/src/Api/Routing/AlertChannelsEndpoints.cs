using Api.BoundedContexts.Administration.Application.Commands.AlertChannels;
using Api.BoundedContexts.Administration.Application.Queries.AlertChannels;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for alert channel configuration (Issue #1840 SP5 F4-C7).
///
/// <list type="bullet">
///   <item><c>GET /api/v1/admin/alert-channels</c> — list email + slack channel state for the Canali drawer</item>
///   <item><c>PUT /api/v1/admin/alert-channels/{type}</c> — upsert a channel config</item>
///   <item><c>POST /api/v1/admin/alert-channels/{type}/test-connection</c> — probe transport</item>
/// </list>
///
/// <para>All endpoints follow the CQRS rule: routing → IMediator.Send only.
/// Auth is enforced via <c>RequireAuthorization()</c> on the group.</para>
/// </summary>
internal static class AlertChannelsEndpoints
{
    public static void MapAlertChannelsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/alert-channels")
            .WithTags("Admin", "AlertChannels")
            .RequireAuthorization();

        // GET /api/v1/admin/alert-channels
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
            {
                var channels = await mediator.Send(new GetAllAlertChannelsQuery(), ct).ConfigureAwait(false);
                return Results.Ok(channels);
            })
            .WithName("AlertChannels_GetAll")
            .WithSummary("List all configured alert channels (email + slack)")
            .WithOpenApi();

        // PUT /api/v1/admin/alert-channels/{type}
        group.MapPut("/{type}", async (
                string type,
                [FromBody] UpsertAlertChannelRequest request,
                HttpContext context,
                IMediator mediator,
                CancellationToken ct) =>
            {
                ArgumentNullException.ThrowIfNull(request);
                var userId = context.User.FindFirst("userId")?.Value ?? "system";

                var command = new UpsertAlertChannelCommand(
                    Type: type,
                    ConfigJson: request.ConfigJson,
                    IsEnabled: request.IsEnabled,
                    RowVersion: request.RowVersion,
                    UpdatedBy: userId);

                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("AlertChannels_Upsert")
            .WithSummary("Create or update an alert channel configuration")
            .WithOpenApi();

        // POST /api/v1/admin/alert-channels/{type}/test-connection
        group.MapPost("/{type}/test-connection", async (
                string type,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var result = await mediator
                    .Send(new TestAlertChannelConnectionCommand(type), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("AlertChannels_TestConnection")
            .WithSummary("Probe the channel's transport (Slack: webhook POST · Email: config sanity-check)")
            .WithOpenApi();
    }
}

/// <summary>
/// Request body for <c>PUT /api/v1/admin/alert-channels/{type}</c>.
/// RowVersion is omitted on first-time creation; required for in-place updates.
/// </summary>
internal sealed record UpsertAlertChannelRequest(string ConfigJson, bool IsEnabled, string? RowVersion);
