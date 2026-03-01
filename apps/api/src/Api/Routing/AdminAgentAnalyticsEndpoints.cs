using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for agent analytics.
/// Issue #4653: Agents analytics for Admin Dashboard.
/// Issue #4917: Admin chat history - replaced static mock with real ChatThread data.
/// </summary>
internal static class AdminAgentAnalyticsEndpoints
{
    public static RouteGroupBuilder MapAdminAgentAnalyticsEndpoints(this RouteGroupBuilder group)
    {
        var agentsGroup = group.MapGroup("/admin/agents")
            .WithTags("Admin", "Agents");

        // GET /api/v1/admin/agents/chat-history (real data - Issue #4917)
        agentsGroup.MapGet("/chat-history", async (
            HttpContext context,
            IMediator mediator,
            string? agentType = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var query = new GetAdminChatSessionsQuery(
                AgentType: agentType,
                DateFrom: dateFrom,
                DateTo: dateTo,
                Page: Math.Max(1, page),
                PageSize: Math.Clamp(pageSize, 1, 100));

            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(new
            {
                sessions = result.Sessions,
                total = result.Total,
                page = result.Page,
                pageSize = result.PageSize,
            });
        })
        .WithName("GetAdminChatHistory")
        .WithSummary("Get admin chat history (real data)")
        .WithDescription("Returns paginated real ChatThread data for admin dashboard. Issue #4917.")
        .Produces(200)
        .Produces(401)
        .Produces(403);

        // GET /api/v1/admin/agents/models (static config)
        agentsGroup.MapGet("/models", (HttpContext context) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var models = new[]
            {
                new { id = "1", provider = "OpenAI", name = "GPT-4 Turbo", enabled = true, costPer1k = 0.01, avgLatency = 1.2, usage = 8420 },
                new { id = "2", provider = "Anthropic", name = "Claude 3.5 Sonnet", enabled = true, costPer1k = 0.003, avgLatency = 1.5, usage = 5230 },
                new { id = "3", provider = "OpenAI", name = "GPT-3.5 Turbo", enabled = true, costPer1k = 0.001, avgLatency = 0.8, usage = 3150 },
                new { id = "4", provider = "Google", name = "Gemini Pro", enabled = false, costPer1k = 0.00025, avgLatency = 1.1, usage = 420 },
                new { id = "5", provider = "Anthropic", name = "Claude 3 Haiku", enabled = true, costPer1k = 0.00025, avgLatency = 0.6, usage = 2840 },
            };

            return Results.Ok(new { models });
        });

        return group;
    }
}
