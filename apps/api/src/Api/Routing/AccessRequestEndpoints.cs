using Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;
using Api.BoundedContexts.Authentication.Application.Queries.AccessRequest;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Access request and registration mode endpoints.
/// Public: registration-mode, request-access
/// Admin: access request management, registration mode toggle
/// </summary>
internal static class AccessRequestEndpoints
{
    public static RouteGroupBuilder MapAccessRequestEndpoints(this RouteGroupBuilder group)
    {
        // Public endpoints (no auth required)
        MapGetRegistrationModeEndpoint(group);
        MapRequestAccessEndpoint(group);

        // Admin endpoints
        MapGetAccessRequestsEndpoint(group);
        MapGetAccessRequestStatsEndpoint(group);
        MapGetAccessRequestByIdEndpoint(group);
        MapApproveAccessRequestEndpoint(group);
        MapRejectAccessRequestEndpoint(group);
        MapBulkApproveEndpoint(group);
        MapSetRegistrationModeEndpoint(group);

        return group;
    }

    // --- Public Endpoints ---

    private static void MapGetRegistrationModeEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/auth/registration-mode", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetRegistrationModeQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("GetRegistrationMode")
          .RequireRateLimiting("AuthLogin");
    }

    private static void MapRequestAccessEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/auth/request-access", async (RequestAccessPayload payload, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new RequestAccessCommand(payload.Email), ct).ConfigureAwait(false);
            // Always return 202 with identical message (email enumeration prevention)
            return Results.Accepted(value: new
            {
                message = "If this email is eligible, you will receive an invitation when approved."
            });
        }).WithName("RequestAccess")
          .RequireRateLimiting("AuthRegister");
    }

    // --- Admin Endpoints ---

    private static void MapGetAccessRequestsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/admin/access-requests", async (
            HttpContext context, IMediator mediator,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new GetAccessRequestsQuery(status, page, Math.Min(pageSize, 100)), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("GetAccessRequests")
          .RequireAdminSession();
    }

    private static void MapGetAccessRequestStatsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/admin/access-requests/stats", async (
            HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetAccessRequestStatsQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("GetAccessRequestStats")
          .RequireAdminSession();
    }

    private static void MapGetAccessRequestByIdEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/admin/access-requests/{id:guid}", async (
            Guid id, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetAccessRequestByIdQuery(id), ct).ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("GetAccessRequestById")
          .RequireAdminSession();
    }

    private static void MapApproveAccessRequestEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/admin/access-requests/{id:guid}/approve", async (
            Guid id, HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            await mediator.Send(
                new ApproveAccessRequestCommand(id, session!.User!.Id), ct).ConfigureAwait(false);
            return Results.Ok(new { status = "approved" });
        }).WithName("ApproveAccessRequest")
          .RequireAdminSession();
    }

    private static void MapRejectAccessRequestEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/admin/access-requests/{id:guid}/reject", async (
            Guid id, RejectAccessRequestPayload? payload,
            HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            await mediator.Send(
                new RejectAccessRequestCommand(id, session!.User!.Id, payload?.Reason), ct)
                .ConfigureAwait(false);
            return Results.Ok(new { status = "rejected" });
        }).WithName("RejectAccessRequest")
          .RequireAdminSession();
    }

    private static void MapBulkApproveEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/admin/access-requests/bulk-approve", async (
            BulkApprovePayload payload,
            HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new BulkApproveAccessRequestsCommand(payload.Ids, session!.User!.Id), ct)
                .ConfigureAwait(false);
            return Results.Ok(result);
        }).WithName("BulkApproveAccessRequests")
          .RequireAdminSession();
    }

    private static void MapSetRegistrationModeEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/admin/settings/registration-mode", async (
            SetRegistrationModePayload payload,
            HttpContext context, IMediator mediator, CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            await mediator.Send(
                new SetRegistrationModeCommand(payload.Enabled, session!.User!.Id), ct)
                .ConfigureAwait(false);
            return Results.Ok(new { publicRegistrationEnabled = payload.Enabled });
        }).WithName("SetRegistrationMode")
          .RequireAdminSession();
    }
}

// Payload records
internal record RequestAccessPayload(string Email);
internal record RejectAccessRequestPayload(string? Reason);
internal record BulkApprovePayload(IReadOnlyList<Guid> Ids);
internal record SetRegistrationModePayload(bool Enabled);
