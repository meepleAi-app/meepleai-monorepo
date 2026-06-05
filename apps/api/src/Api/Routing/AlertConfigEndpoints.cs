using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Application.Queries.AlertRules;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Alert configuration endpoints (Issue #921)
/// </summary>
internal static class AlertConfigEndpoints
{
    public static void MapAlertConfigEndpoints(this IEndpointRouteBuilder app)
    {
        // Note: Program.cs invokes this on the `v1Api` route group which already
        // applies the `/api/v1` prefix. The group path is therefore the relative
        // segment only. Hardcoding `/api/v1` here would produce
        // `/api/v1/api/v1/admin/alert-rules` (double-prefix) — fix #1840 also
        // addresses this pre-existing latent routing bug from issue #921 because
        // the new `MapTestAlertRuleEndpoint` is registered inside this group and
        // would otherwise inherit the same broken path.
        var group = app.MapGroup("/admin/alert-rules").WithTags("Admin", "AlertRules");

        MapGetAllAlertRulesEndpoint(group);
        MapGetAlertRuleByIdEndpoint(group);
        MapCreateAlertRuleEndpoint(group);
        MapUpdateAlertRuleEndpoint(group);
        MapDeleteAlertRuleEndpoint(group);
        MapToggleAlertRuleEndpoint(group);
        MapTestAlertRuleEndpoint(group);

        MapGlobalAdminEndpoints(app);
    }

    private static void MapGetAllAlertRulesEndpoint(RouteGroupBuilder group)
    {
        // GET /api/v1/admin/alert-rules
        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var rules = await mediator.Send(new GetAllAlertRulesQuery(), ct).ConfigureAwait(false);
            return Results.Ok(rules);
        })
        .RequireAuthorization()
        .WithName("GetAllAlertRules")
        .WithOpenApi();
    }

    private static void MapGetAlertRuleByIdEndpoint(RouteGroupBuilder group)
    {
        // GET /api/v1/admin/alert-rules/{id}
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var rule = await mediator.Send(new GetAlertRuleByIdQuery(id), ct).ConfigureAwait(false);
            return rule == null ? Results.NotFound() : Results.Ok(rule);
        })
        .RequireAuthorization()
        .WithName("GetAlertRuleById")
        .WithOpenApi();
    }

    private static void MapCreateAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // POST /api/v1/admin/alert-rules
        group.MapPost("/", async ([FromBody] CreateAlertRuleRequest request, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var userId = context.User.FindFirst("userId")?.Value ?? "system";

            var command = new CreateAlertRuleCommand(
                request.Name,
                request.AlertType,
                request.Severity,
                request.ThresholdValue,
                request.ThresholdUnit,
                request.DurationMinutes,
                request.Description,
                userId
            );

            var id = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/admin/alert-rules/{id}", new { id });
        })
        .RequireAuthorization()
        .WithName("CreateAlertRule")
        .WithOpenApi();
    }

    private static void MapUpdateAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // PUT /api/v1/admin/alert-rules/{id}
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateAlertRuleRequest request, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var userId = context.User.FindFirst("userId")?.Value ?? "system";

            var command = new UpdateAlertRuleCommand(
                id,
                request.Name,
                request.Severity,
                request.ThresholdValue,
                request.ThresholdUnit,
                request.DurationMinutes,
                request.Description,
                userId
            );

            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("UpdateAlertRule")
        .WithOpenApi();
    }

    private static void MapDeleteAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // DELETE /api/v1/admin/alert-rules/{id}
        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new DeleteAlertRuleCommand(id), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("DeleteAlertRule")
        .WithOpenApi();
    }

    private static void MapToggleAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // PATCH /api/v1/admin/alert-rules/{id}/toggle
        group.MapPatch("/{id:guid}/toggle", async (Guid id, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var userId = context.User.FindFirst("userId")?.Value ?? "system";

            await mediator.Send(new EnableAlertRuleCommand(id, userId), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("ToggleAlertRule")
        .WithOpenApi();
    }

    private static void MapTestAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // POST /api/v1/admin/alert-rules/{id}/test?mode=dryRun|live
        // Issue #1840 SP5 F4-C7 — per-rule TestAlert action. Default mode is
        // "dryRun" (safe by default per spec decision D3); explicit
        // ?mode=live requires the FE to gate behind a confirm dialog.
        //
        // Auth: RequireAdminSession() enforces both session + Admin role check.
        // The plain RequireAuthorization() used by the legacy sibling endpoints
        // in this file only enforces authentication and would allow any signed-in
        // user — tracked as a follow-up (legacy hardening, out-of-scope for #1840).
        group.MapPost("/{id:guid}/test", async (
                Guid id,
                [FromQuery] string? mode,
                HttpContext context,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var userId = context.User.FindFirst("userId")?.Value ?? "system";
                var result = await mediator
                    .Send(new TestAlertRuleCommand(id, mode, userId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .RequireAdminSession()
            .WithName("TestAlertRule")
            .WithSummary("Probe an existing alert rule by raising a synthetic AlertFiredEvent")
            .WithOpenApi();
    }

    private static void MapGlobalAdminEndpoints(IEndpointRouteBuilder app)
    {
        // Same fix as the group above — `app` here is `v1Api` (already `/api/v1`),
        // so we use relative paths to avoid the `/api/v1/api/v1/...` double prefix.
        // GET /api/v1/admin/alert-templates
        app.MapGet("/admin/alert-templates", async (IMediator mediator, CancellationToken ct) =>
        {
            var templates = await mediator.Send(new GetAlertTemplatesQuery(), ct).ConfigureAwait(false);
            return Results.Ok(templates);
        })
        .RequireAuthorization()
        .WithTags("Admin", "AlertRules")
        .WithName("GetAlertTemplates")
        .WithOpenApi();

        // POST /api/v1/admin/alert-test
        app.MapPost("/admin/alert-test", async ([FromBody] TestAlertRequest request, IMediator mediator, CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var success = await mediator.Send(new TestAlertCommand(request.AlertType, request.Channel), ct).ConfigureAwait(false);
            return success ? Results.Ok(new { success = true }) : Results.BadRequest(new { success = false, error = "Test alert failed" });
        })
        .RequireAuthorization()
        .WithTags("Admin", "AlertRules")
        .WithName("TestAlert")
        .WithOpenApi();
    }
}

internal record CreateAlertRuleRequest(string Name, string AlertType, string Severity, double ThresholdValue, string ThresholdUnit, int DurationMinutes, string? Description);
internal record UpdateAlertRuleRequest(string Name, string Severity, double ThresholdValue, string ThresholdUnit, int DurationMinutes, string? Description);
internal record TestAlertRequest(string AlertType, string Channel);
