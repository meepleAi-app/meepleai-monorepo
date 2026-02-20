using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Filters;
using Api.Infrastructure.Serialization;
using Api.Models;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoint for real-time RAG pipeline debug chat.
/// Streams normal chat events interleaved with debug events for pipeline tracing.
/// </summary>
internal static class AdminDebugChatEndpoints
{
    public static void MapAdminDebugChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/agents/debug-chat")
            .WithTags("Admin - Debug Chat")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/stream", HandleDebugStreamAsync)
            .WithName("AdminDebugChatStream")
            .WithSummary("Stream RAG Q&A with real-time pipeline debug events (Admin)");
    }

    private static async Task<IResult> HandleDebugStreamAsync(
        DebugChatRequest req,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.gameId))
            return Results.BadRequest(new { error = "gameId is required" });

        if (!Guid.TryParse(req.gameId, out _))
            return Results.BadRequest(new { error = "gameId must be a valid GUID" });

        if (string.IsNullOrWhiteSpace(req.query))
            return Results.BadRequest(new { error = "query is required" });

        logger.LogInformation("[DebugChat] Admin debug chat request for game {GameId}: {Query}, strategy: {Strategy}",
            req.gameId, req.query, req.strategyOverride ?? "default");

        // Set SSE headers
        context.Response.Headers["Content-Type"] = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["Connection"] = "keep-alive";

        try
        {
            var query = new StreamDebugQaQuery(
                req.gameId,
                req.query,
                req.chatId,
                req.documentIds,
                req.strategyOverride,
                req.includePrompts);

            await foreach (var evt in mediator.CreateStream(query, ct).ConfigureAwait(false))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(evt, SseJsonOptions.Default);
                await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            logger.LogInformation(ex, "[DebugChat] Stream cancelled by client for game {GameId}", req.gameId);
        }
#pragma warning disable CA1031 // SSE endpoint must handle all errors gracefully
        catch (Exception ex)
        {
            logger.LogError(ex, "[DebugChat] Error during debug stream for game {GameId}", req.gameId);

            try
            {
                var errorEvent = new RagStreamingEvent(
                    StreamingEventType.Error,
                    new StreamingError("An internal error occurred. See server logs for details.", "INTERNAL_ERROR"),
                    DateTime.UtcNow);
                var json = System.Text.Json.JsonSerializer.Serialize(errorEvent, SseJsonOptions.Default);
                await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Cleanup: if error event fails, client is disconnected
            catch
            {
                // Client connection broken, nothing more we can do
            }
#pragma warning restore CA1031
        }
#pragma warning restore CA1031

        return Results.Empty;
    }
}
