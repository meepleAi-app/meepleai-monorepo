using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Unified gateway endpoint for all agent interactions.
/// Issue #4338: Unified API Gateway - Single entry point for multi-agent system.
/// </summary>
internal static class UnifiedAgentGatewayEndpoints
{
    public static void MapUnifiedAgentGatewayEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/v1/agents/query - Unified entry point
        app.MapPost("/api/v1/agents/query", async (
            UnifiedQueryRequest req,
            HttpContext context,
            AgentRouterService router,
            AgentStateCoordinator coordinator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // 1. Classify intent and route to appropriate agent
            var routing = router.RouteQuery(req.Query);

            logger.LogInformation(
                "[Gateway] Query routed: intent={Intent}, agent={Agent}, confidence={Confidence:F2}",
                routing.Intent,
                routing.TargetAgent,
                routing.Confidence);

            // 2. Load shared context for agent
            var sharedContext = await coordinator.GetSharedContextAsync(
                req.GameSessionId,
                req.GameId,
                ct).ConfigureAwait(false);

            // 3. Return routing decision (actual agent invocation handled by client)
            return Results.Ok(new
            {
                RoutedTo = routing.TargetAgent,
                Intent = routing.Intent.ToString(),
                Confidence = routing.Confidence,
                ShouldRoute = routing.ShouldRoute,
                RequiresConfirmation = routing.RequiresConfirmation,
                FallbackAgents = routing.FallbackAgents,
                SharedContext = new
                {
                    sharedContext.SessionId,
                    sharedContext.GameId,
                    ConversationTurns = sharedContext.ConversationHistory.Count,
                    sharedContext.StateVersion
                },
                Message = routing.ShouldRoute
                    ? $"Query routed to {routing.TargetAgent}"
                    : "Please select an agent or rephrase query"
            });
        })
        .RequireSession()
        .WithName("UnifiedAgentQuery")
        .WithTags("Agents", "Gateway")
        .WithSummary("Unified agent query gateway")
        .WithDescription("Single entry point for all agent interactions with intelligent routing.");
    }
}

internal record UnifiedQueryRequest(
    string Query,
    Guid GameSessionId,
    Guid GameId
);
