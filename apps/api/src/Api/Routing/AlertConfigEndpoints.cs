using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Application.Queries.AlertRules;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using Api.Filters;
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
        //
        // #4568: every route of the group was guarded by a bare RequireAuthorization(), so any
        // signed-in user could list, create, edit, toggle and delete alert rules. The admin
        // filter on the group answers 401/403 before any handler runs.
        var group = app.MapGroup("/admin/alert-rules")
            .WithTags("Admin", "AlertRules")
            .AddEndpointFilter<RequireAdminSessionFilter>();

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
        .WithName("GetAllAlertRules");
    }

    private static void MapGetAlertRuleByIdEndpoint(RouteGroupBuilder group)
    {
        // GET /api/v1/admin/alert-rules/{id}
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var rule = await mediator.Send(new GetAlertRuleByIdQuery(id), ct).ConfigureAwait(false);
            return rule == null ? Results.NotFound() : Results.Ok(rule);
        })
        .WithName("GetAlertRuleById");
    }

    private static void MapCreateAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // POST /api/v1/admin/alert-rules
        group.MapPost("/", async ([FromBody] CreateAlertRuleRequest request, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var userId = AuthorId(context);

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
        .WithName("CreateAlertRule");
    }

    private static void MapUpdateAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // PUT /api/v1/admin/alert-rules/{id}
        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateAlertRuleRequest request, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var userId = AuthorId(context);

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
        .WithName("UpdateAlertRule");
    }

    private static void MapDeleteAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // DELETE /api/v1/admin/alert-rules/{id}
        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new DeleteAlertRuleCommand(id), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("DeleteAlertRule");
    }

    private static void MapToggleAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // PATCH /api/v1/admin/alert-rules/{id}/toggle
        group.MapPatch("/{id:guid}/toggle", async (Guid id, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var userId = AuthorId(context);

            await mediator.Send(new EnableAlertRuleCommand(id, userId), ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName("ToggleAlertRule");
    }

    private static void MapTestAlertRuleEndpoint(RouteGroupBuilder group)
    {
        // POST /api/v1/admin/alert-rules/{id}/test?mode=dryRun|live
        // Issue #1840 SP5 F4-C7 — per-rule TestAlert action. Default mode is
        // "dryRun" (safe by default per spec decision D3); explicit
        // ?mode=live requires the FE to gate behind a confirm dialog.
        //
        // Auth: the admin filter on the group (#4568). This route carried its own
        // RequireAdminSession() while its siblings had only RequireAuthorization().
        group.MapPost("/{id:guid}/test", async (
                Guid id,
                [FromQuery] string? mode,
                HttpContext context,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var userId = AuthorId(context);
                var result = await mediator
                    .Send(new TestAlertRuleCommand(id, mode, userId), ct)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            })
            .WithName("TestAlertRule")
            .WithSummary("Probe an existing alert rule by raising a synthetic AlertFiredEvent");
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
        .RequireAdminSession() // #4568: was a bare RequireAuthorization()
        .WithTags("Admin", "AlertRules")
        .WithName("GetAlertTemplates");

        // POST /api/v1/admin/alert-test
        app.MapPost("/admin/alert-test", async ([FromBody] TestAlertRequest request, IMediator mediator, CancellationToken ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var success = await mediator.Send(new TestAlertCommand(request.AlertType, request.Channel), ct).ConfigureAwait(false);
            return success ? Results.Ok(new { success = true }) : Results.BadRequest(new { success = false, error = "Test alert failed" });
        })
        .RequireAdminSession() // #4568: was a bare RequireAuthorization()
        .WithTags("Admin", "AlertRules")
        .WithName("TestAlert");
    }

    /// <summary>
    /// The author recorded on a rule: the admin who made the request. The admin filter has stored
    /// the session; audit attribution reads EffectiveActor (Principal.cs), the real admin even
    /// during an impersonation. #4568: these endpoints read a "userId" claim that the session
    /// handler does not emit, so every rule said "system".
    /// </summary>
    private static string AuthorId(HttpContext context) =>
        ((SessionStatusDto)context.Items[nameof(SessionStatusDto)]!).Principal!.EffectiveActor.Id.ToString();
}

internal record CreateAlertRuleRequest(string Name, string AlertType, string Severity, double ThresholdValue, string ThresholdUnit, int DurationMinutes, string? Description);
internal record UpdateAlertRuleRequest(string Name, string Severity, double ThresholdValue, string ThresholdUnit, int DurationMinutes, string? Description);
internal record TestAlertRequest(string AlertType, string Channel);
