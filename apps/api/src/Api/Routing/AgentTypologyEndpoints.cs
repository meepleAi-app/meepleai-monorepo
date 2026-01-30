using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Agent Typology endpoints for proposal and testing workflows.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
internal static class AgentTypologyEndpoints
{
    public static RouteGroupBuilder MapAgentTypologyEndpoints(this RouteGroupBuilder group)
    {
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
