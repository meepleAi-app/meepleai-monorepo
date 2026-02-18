using Api.Extensions;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for agent analytics (static/prototype data).
/// Issue #4653: Agents analytics for Admin Dashboard.
/// Note: Using static data for fast prototyping. Real implementation TBD.
/// </summary>
internal static class AdminAgentAnalyticsEndpoints
{
    public static RouteGroupBuilder MapAdminAgentAnalyticsEndpoints(this RouteGroupBuilder group)
    {
        var agentsGroup = group.MapGroup("/admin/agents")
            .WithTags("Admin", "Agents");

        // GET /api/v1/admin/agents/chat-history (static prototype)
        agentsGroup.MapGet("/chat-history", (HttpContext context) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            // Static data for prototype - real query implementation pending
            var sessions = new[]
            {
                new { id = "chat-001", userId = "user-123", userName = "Sarah Chen", agent = "Rules Expert", messageCount = 12, duration = 245, satisfaction = 5, date = DateTime.UtcNow.AddHours(-2), preview = Array.Empty<object>() },
                new { id = "chat-002", userId = "user-456", userName = "Mike Johnson", agent = "Strategy Advisor", messageCount = 8, duration = 180, satisfaction = 4, date = DateTime.UtcNow.AddHours(-5), preview = Array.Empty<object>() },
            };

            return Results.Ok(new { sessions, total = sessions.Length, page = 1, pageSize = 20 });
        });

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

