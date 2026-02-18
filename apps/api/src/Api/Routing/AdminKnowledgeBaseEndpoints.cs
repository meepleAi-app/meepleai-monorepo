using Api.Extensions;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for Knowledge Base management (static/prototype).
/// Issues #4654, #4655: KB endpoints for Admin Dashboard.
/// </summary>
internal static class AdminKnowledgeBaseEndpoints
{
    public static RouteGroupBuilder MapAdminKnowledgeBaseEndpoints(this RouteGroupBuilder group)
    {
        var kbGroup = group.MapGroup("/admin/kb")
            .WithTags("Admin", "KnowledgeBase");

        // GET /api/v1/admin/kb/vector-collections (#4655)
        kbGroup.MapGet("/vector-collections", (HttpContext context) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var collections = new[]
            {
                new { name = "Game Rules", vectorCount = 42500, dimensions = 384, storage = "3.2 GB", health = 98 },
                new { name = "Strategy Guides", vectorCount = 28300, dimensions = 384, storage = "2.1 GB", health = 95 },
                new { name = "FAQ Database", vectorCount = 9200, dimensions = 384, storage = "1.5 GB", health = 92 },
            };

            return Results.Ok(new { collections });
        });

        // GET /api/v1/admin/kb/processing-queue (#4655)
        kbGroup.MapGet("/processing-queue", (HttpContext context) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var queue = new[]
            {
                new { id = "1", fileName = "catan-rulebook.pdf", status = "processing", progress = 65, size = "2.4 MB" },
                new { id = "2", fileName = "wingspan-strategy.pdf", status = "completed", progress = 100, size = "1.8 MB" },
                new { id = "3", fileName = "pandemic-faq.docx", status = "processing", progress = 35, size = "0.9 MB" },
            };

            return Results.Ok(new { queue });
        });

        // GET /api/v1/admin/shared-games (extended for admin - #4654)
        var gamesGroup = group.MapGroup("/admin/shared-games")
            .WithTags("Admin", "SharedGames");

        gamesGroup.MapGet("/categories", (HttpContext context) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var categories = new[]
            {
                new { id = "1", name = "Strategy", emoji = "♟️", gameCount = 42, color = "#3b82f6" },
                new { id = "2", name = "Party", emoji = "🎉", gameCount = 28, color = "#ec4899" },
                new { id = "3", name = "Cooperative", emoji = "🤝", gameCount = 19, color = "#10b981" },
                new { id = "4", name = "Deck Building", emoji = "🃏", gameCount = 15, color = "#8b5cf6" },
                new { id = "5", name = "Family", emoji = "👨\u200D👩\u200D👧\u200D👦", gameCount = 34, color = "#f59e0b" },
                new { id = "6", name = "Abstract", emoji = "🔷", gameCount = 12, color = "#06b6d4" },
                new { id = "7", name = "Thematic", emoji = "🗺️", gameCount = 23, color = "#ef4444" },
                new { id = "8", name = "Euro", emoji = "🏛️", gameCount = 31, color = "#6366f1" },
            };

            return Results.Ok(new { categories });
        });

        return group;
    }
}
