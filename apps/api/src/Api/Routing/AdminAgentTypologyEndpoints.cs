using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin-scoped endpoints for agent typology management.
/// Provides full CRUD and approval workflow for admin dashboard.
/// </summary>
internal static class AdminAgentTypologyEndpoints
{
    public static RouteGroupBuilder MapAdminAgentTypologyEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/admin/agent-typologies
        // Admin: Get all agent typologies (no filtering)
        group.MapGet("/", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAllAgentTypologiesQuery(
                UserRole: "Admin",
                UserId: session!.User!.Id);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("AdminGetAllAgentTypologies")
        .WithTags("Admin", "AgentTypology")
        .WithSummary("Get all agent typologies (Admin)")
        .WithDescription("Returns all agent typologies without role-based filtering");

        // GET /api/v1/admin/agent-typologies/{id}
        // Admin: Get single agent typology by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetTypologyByIdQuery(
                TypologyId: id,
                UserRole: "Admin",
                UserId: session!.User!.Id);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("AdminGetAgentTypologyById")
        .WithTags("Admin", "AgentTypology")
        .WithSummary("Get agent typology by ID (Admin)")
        .WithDescription("Returns a single agent typology by ID without visibility restrictions");

        // DELETE /api/v1/admin/agent-typologies/{id}
        // Admin: Soft delete an agent typology
        group.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new DeleteAgentTypologyCommand(Id: id);

            await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.NoContent();
        })
        .WithName("AdminDeleteAgentTypology")
        .WithTags("Admin", "AgentTypology")
        .WithSummary("Delete agent typology (Admin)")
        .WithDescription("Soft deletes an agent typology");

        // POST /api/v1/admin/agent-typologies/{id}/approve
        // Admin: Approve a pending agent typology
        group.MapPost("/{id:guid}/approve", async (
            Guid id,
            ApproveAgentTypologyRequest? request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new ApproveAgentTypologyCommand(
                Id: id,
                ApprovedBy: session!.User!.Id,
                Notes: request?.Notes);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("AdminApproveAgentTypology")
        .WithTags("Admin", "AgentTypology")
        .WithSummary("Approve agent typology (Admin)")
        .WithDescription("Transitions a pending agent typology to Approved status");

        // POST /api/v1/admin/agent-typologies/{id}/reject
        // Admin: Reject a pending agent typology
        group.MapPost("/{id:guid}/reject", async (
            Guid id,
            RejectAgentTypologyRequest request,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var command = new RejectAgentTypologyCommand(
                Id: id,
                RejectedBy: session!.User!.Id,
                Reason: request.Reason);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("AdminRejectAgentTypology")
        .WithTags("Admin", "AgentTypology")
        .WithSummary("Reject agent typology (Admin)")
        .WithDescription("Transitions a pending agent typology to Rejected status with a reason");

        return group;
    }
}

/// <summary>
/// Request model for approving an agent typology.
/// </summary>
internal record ApproveAgentTypologyRequest(string? Notes = null);

/// <summary>
/// Request model for rejecting an agent typology.
/// </summary>
internal record RejectAgentTypologyRequest(string Reason);
