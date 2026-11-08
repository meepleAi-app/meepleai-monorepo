using Api.BoundedContexts.Administration.Application.Commands.AlertConfiguration;
using Api.BoundedContexts.Administration.Application.Queries.AlertConfiguration;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Alert configuration endpoints (Issue #915)
/// Manages Email, Slack, PagerDuty configuration for alerting system
/// </summary>
internal static class AlertConfigurationEndpoints
{
    public static void MapAlertConfigurationEndpoints(this IEndpointRouteBuilder app)
    {
        // #4568: the values are SMTP hosts, Slack webhooks and PagerDuty keys. A bare
        // RequireAuthorization() let any signed-in user read and write them; the admin
        // filter answers 401 to anonymous callers and 403 to non-admins before any handler.
        var group = app.MapGroup("/admin/alert-configuration")
            .WithTags("Admin", "AlertConfiguration")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        // GET /api/v1/admin/alert-configuration
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var configurations = await mediator.Send(new GetAllAlertConfigurationsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(configurations);
        })
        .WithName("GetAllAlertConfigurations")
        .WithSummary("Get all alert configurations");

        // GET /api/v1/admin/alert-configuration/{category}
        group.MapGet("/{category}", async (string category, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var configuration = await mediator.Send(new GetAlertConfigurationQuery(category), ct).ConfigureAwait(false);
                return Results.Ok(configuration);
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(new { message = $"No configuration found for category: {category}" });
            }
        })
        .WithName("GetAlertConfigurationByCategory")
        .WithSummary("Get alert configuration by category");

        // PUT /api/v1/admin/alert-configuration
        group.MapPut("/", async (
            [FromBody] UpdateAlertConfigurationRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // #4568: the session handler emits no "userId" claim, so the author was always
            // "system". SessionAuthenticationMiddleware stores the session in Items, and the admin
            // filter has already required it to be valid, so the cast cannot fail here.
            // EffectiveActor (Principal.cs) is the real admin even during an impersonation.
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var updatedBy = session.Principal!.EffectiveActor.Id.ToString();

            var command = new UpdateAlertConfigurationCommand(
                request.ConfigKey,
                request.ConfigValue,
                request.Category,
                updatedBy,
                request.Description);

            var success = await mediator.Send(command, ct).ConfigureAwait(false);

            return success
                ? Results.Ok(new { message = "Configuration updated successfully" })
                : Results.BadRequest(new { message = "Failed to update configuration" });
        })
        .WithName("UpdateAlertConfiguration")
        .WithSummary("Update or create alert configuration");
    }
}

/// <summary>
/// Request DTO for updating alert configuration (Issue #915)
/// </summary>
internal record UpdateAlertConfigurationRequest(
    string ConfigKey,
    string ConfigValue,
    string Category,
    string? Description);
