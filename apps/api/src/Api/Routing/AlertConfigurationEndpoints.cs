using Api.BoundedContexts.Administration.Application.Commands.AlertConfiguration;
using Api.BoundedContexts.Administration.Application.Queries.AlertConfiguration;
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
        var group = app.MapGroup("/api/v1/admin/alert-configuration")
            .WithTags("Admin", "AlertConfiguration")
            .RequireAuthorization();

        // GET /api/v1/admin/alert-configuration
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var configurations = await mediator.Send(new GetAllAlertConfigurationsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(configurations);
        })
        .WithName("GetAllAlertConfigurations")
        .WithSummary("Get all alert configurations")
        .WithOpenApi();

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
        .WithSummary("Get alert configuration by category")
        .WithOpenApi();

        // PUT /api/v1/admin/alert-configuration
        group.MapPut("/", async (
            [FromBody] UpdateAlertConfigurationRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = context.User.FindFirst("userId")?.Value ?? "system";

            var command = new UpdateAlertConfigurationCommand(
                request.ConfigKey,
                request.ConfigValue,
                request.Category,
                userId,
                request.Description);

            var success = await mediator.Send(command, ct).ConfigureAwait(false);

            return success
                ? Results.Ok(new { message = "Configuration updated successfully" })
                : Results.BadRequest(new { message = "Failed to update configuration" });
        })
        .WithName("UpdateAlertConfiguration")
        .WithSummary("Update or create alert configuration")
        .WithOpenApi();
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
