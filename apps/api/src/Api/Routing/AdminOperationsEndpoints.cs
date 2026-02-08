using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands.Operations;
using Api.BoundedContexts.Administration.Application.Queries.Operations;
using Api.Extensions;
using Api.Infrastructure.Authorization;
using MediatR;
using Administration = Api.BoundedContexts.Administration;

namespace Api.Routing;

/// <summary>
/// Admin operations endpoints for service control panel.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
internal static class AdminOperationsEndpoints
{
    public static RouteGroupBuilder MapAdminOperationsEndpoints(this RouteGroupBuilder group)
    {
        var operationsGroup = group.MapGroup("/admin/operations")
            .WithTags("Admin", "Operations");

        // Service Health Monitoring
        operationsGroup.MapGet("/health", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetServiceHealthQuery();
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get service health status")
        .WithDescription("Returns aggregated health status for all monitored services");

        operationsGroup.MapGet("/metrics", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetServiceMetricsQuery();
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get service performance metrics")
        .WithDescription("Returns uptime, latency, and request count for all services");

        // Email Management
        operationsGroup.MapGet("/emails", async (
            HttpContext context,
            IMediator mediator,
            int? limit,
            int? offset,
            DateTime? startDate,
            DateTime? endDate,
            string? status,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetSentEmailsQuery(
                Limit: limit ?? 50,
                Offset: offset ?? 0,
                StartDate: startDate,
                EndDate: endDate,
                Status: status
            );

            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get sent email records")
        .WithDescription("Returns sent email logs with pagination and filtering");

        // Service Restart (SuperAdmin only, Level 2 confirmation)
        operationsGroup.MapPost("/restart-service", async (
            HttpContext context,
            IMediator mediator,
            RestartServiceRequest request,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var command = new RestartServiceCommand(
                ServiceName: request.ServiceName,
                AdminUserId: session!.User!.Id
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithMetadata(new RequiresConfirmationAttribute(
            ConfirmationLevel.Level2,
            "Restart API Service",
            "This will restart the API service. All active sessions will be terminated. Estimated downtime: 30-60 seconds."))
        .WithSummary("Restart API service")
        .WithDescription("Triggers graceful shutdown; orchestrator handles restart. SuperAdmin only, Level 2 confirmation required.");

        // User Impersonation (SuperAdmin only, Level 2 confirmation)
        operationsGroup.MapPost("/impersonate", async (
            HttpContext context,
            IMediator mediator,
            ImpersonateUserRequest request,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var command = new ImpersonateUserCommand(
                TargetUserId: request.TargetUserId,
                AdminUserId: session!.User!.Id
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithMetadata(new RequiresConfirmationAttribute(
            ConfirmationLevel.Level2,
            "Impersonate User",
            "You will gain full access to this user's account. All actions will be audited."))
        .WithSummary("Impersonate user")
        .WithDescription("Creates session as target user for debugging. SuperAdmin only, Level 2 confirmation required.");

        // End Impersonation (SuperAdmin only)
        operationsGroup.MapPost("/end-impersonation", async (
            HttpContext context,
            IMediator mediator,
            EndImpersonationByIdRequest request,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var command = new Administration.Application.Commands.EndImpersonationCommand(
                SessionId: request.SessionId,
                AdminUserId: session!.User!.Id
            );

            var success = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(new EndImpersonationResponse(success, "Impersonation session ended successfully"));
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("End impersonation session")
        .WithDescription("Revokes impersonation session and creates audit log. SuperAdmin only.");

        return group;
    }
}

// Request DTOs
internal record RestartServiceRequest(string ServiceName);
internal record ImpersonateUserRequest(Guid TargetUserId);
internal record EndImpersonationByIdRequest(Guid SessionId);
