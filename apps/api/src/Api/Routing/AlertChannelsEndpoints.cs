using Api.BoundedContexts.Administration.Application.Commands.AlertChannels;
using Api.BoundedContexts.Administration.Application.Queries.AlertChannels;
using Api.Extensions;
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
/// Auth is enforced via <c>RequireAdminSession()</c> on the group (Admin/SuperAdmin role check,
/// matching the convention used by sibling admin endpoints e.g. <c>AdminMetricsEndpoints</c>,
/// <c>AdminAgentAnalyticsEndpoints</c>). The plain <c>RequireAuthorization()</c> only enforces
/// authentication and would allow any signed-in user.</para>
///
/// <para><b>Secret masking follow-up</b>: the <c>ConfigJson</c> field is returned verbatim
/// to allow the Canali drawer to round-trip without re-fetching. Defense-in-depth masking of
/// Slack <c>webhookUrl</c> in GET responses is deferred to a follow-up issue together with the
/// secret-rotation flow on PUT (mask in transit, require explicit "rotate" intent to change).
/// Admin-only access enforced here limits exposure today.</para>
/// </summary>
internal static class AlertChannelsEndpoints
{
    public static void MapAlertChannelsEndpoints(this IEndpointRouteBuilder app)
    {
        // Note: Program.cs invokes this on the `v1Api` route group which already
        // applies the `/api/v1` prefix. The group path is therefore the relative
        // segment only (matching the AdminMetricsEndpoints pattern). Hardcoding
        // `/api/v1` here would produce `/api/v1/api/v1/admin/alert-channels`
        // (double-prefix), causing all FE calls to 404.
        var group = app.MapGroup("/admin/alert-channels")
            .WithTags("Admin", "AlertChannels");

        // GET /api/v1/admin/alert-channels
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
            {
                var channels = await mediator.Send(new GetAllAlertChannelsQuery(), ct).ConfigureAwait(false);
                return Results.Ok(channels);
            })
            .RequireAdminSession()
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
            .RequireAdminSession()
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
            .RequireAdminSession()
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
