using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Agent Typology endpoints for queries, proposals, and testing.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// Issue #3178: AGT-004 Typology Query Handlers.
/// </summary>
internal static class AgentTypologyEndpoints
{
    public static RouteGroupBuilder MapAgentTypologyEndpoints(this RouteGroupBuilder group)
    {
        // ========================================
        // QUERY ENDPOINTS (AGT-004)
        // ========================================

        // GET /api/v1/agent-typologies
        // Get all agent typologies with role-based filtering
        group.MapGet("/", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.TryGetActiveSession();
            if (!authorized) return error!;

            var query = new GetAllAgentTypologiesQuery(
                UserRole: session.User!.Role,
                UserId: session.User.Id);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("GetAllAgentTypologies")
        .WithTags("AgentTypology")
        .WithSummary("Get all agent typologies (role-filtered)")
        .WithDescription("Returns typologies based on user role: User=approved only, Editor=approved+own, Admin=all");

        // GET /api/v1/agent-typologies/{id}
        // Get single agent typology by ID with visibility check
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.TryGetActiveSession();
            if (!authorized) return error!;

            var query = new GetTypologyByIdQuery(
                TypologyId: id,
                UserRole: session.User!.Role,
                UserId: session.User.Id);

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return result != null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetAgentTypologyById")
        .WithTags("AgentTypology")
        .WithSummary("Get agent typology by ID (role-checked)")
        .WithDescription("Returns typology if visible to user based on role and ownership");

        // GET /api/v1/agent-typologies/pending
        // Admin-only: Get pending typologies awaiting approval
        group.MapGet("/pending", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetPendingTypologiesQuery();
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("GetPendingTypologies")
        .WithTags("AgentTypology", "Admin")
        .WithSummary("Get pending typologies for approval (Admin only)")
        .WithDescription("Returns all typologies with Pending status, ordered by creation date");

        // GET /api/v1/agent-typologies/my-proposals
        // Editor-only: Get own proposals
        group.MapGet("/my-proposals", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            var query = new GetMyProposalsQuery(EditorId: session.User!.Id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("GetMyProposals")
        .WithTags("AgentTypology", "Editor")
        .WithSummary("Get my own typology proposals (Editor)")
        .WithDescription("Returns all typologies created by the authenticated Editor");

        // ========================================
        // COMMAND ENDPOINTS (AGT-003)
        // ========================================

        // POST /api/v1/agent-typologies/propose
        // Editor proposes a new agent typology (creates as Draft)
        group.MapPost("/propose", async (
            ProposeAgentTypologyCommand command,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "User {UserId} ({Role}) proposing agent typology: {TypologyName}",
                session.User!.Id,
                session.User.Role,
                command.Name);

            // Override ProposedBy with authenticated user ID (prevent impersonation)
            var validatedCommand = command with { ProposedBy = session.User.Id };

            var result = await mediator.Send(validatedCommand, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Agent typology proposed: {TypologyId} by user {UserId}",
                result.Id,
                session.User.Id);

            return Results.Created($"/api/v1/agent-typologies/{result.Id}", result);
        })
        .WithName("ProposeAgentTypology")
        .WithTags("AgentTypology", "Editor")
        .WithSummary("Propose a new agent typology (Editor)")
        .WithDescription("Editors can propose agent typologies which are created as Draft status and require Admin approval");

        // POST /api/v1/agent-typologies/{id}/test
        // Editor tests a Draft agent typology in sandbox
        group.MapPost("/{id:guid}/test", async (
            Guid id,
            TestQueryRequest request,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authorized, session, error) = context.RequireAdminOrEditorSession();
            if (!authorized) return error!;

            logger.LogInformation(
                "User {UserId} ({Role}) testing agent typology: {TypologyId}",
                session.User!.Id,
                session.User.Role,
                id);

            var command = new TestAgentTypologyCommand(
                TypologyId: id,
                TestQuery: request.TestQuery,
                RequestedBy: session.User.Id);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Agent typology test failed: {TypologyId}, Error: {ErrorMessage}",
                    id,
                    result.ErrorMessage);

                return Results.BadRequest(new { success = false, error = result.ErrorMessage });
            }

            logger.LogInformation(
                "Agent typology tested successfully: {TypologyId}, Confidence: {Confidence}",
                id,
                result.ConfidenceScore);

            return Results.Ok(new
            {
                success = result.Success,
                response = result.Response,
                confidenceScore = result.ConfidenceScore
            });
        })
        .WithName("TestAgentTypology")
        .WithTags("AgentTypology", "Editor")
        .WithSummary("Test a Draft agent typology in sandbox (Editor)")
        .WithDescription("Editors can test their own Draft typologies to validate configuration before submitting for approval");

        return group;
    }
}

/// <summary>
/// Request model for testing agent typology.
/// </summary>
internal record TestQueryRequest(string TestQuery);
