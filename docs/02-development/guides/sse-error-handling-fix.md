# SSE Error Handling Fix Guide

## Problem
Issue #1194 removed critical try-catch error handling from SSE streaming endpoints, causing:
- Middleware to corrupt SSE responses (writing JSON after text/event-stream headers sent)
- Normal client cancellations to appear as 500 errors
- SSE clients unable to receive error events

## Affected Files
`apps/api/src/Api/Routing/AiEndpoints.cs` - 3 streaming endpoints

---

## Fix #1: `/agents/explain/stream` (lines 318-335)

### FIND THIS CODE (lines 318-335):
```csharp
            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            // Set SSE headers
            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            var query = new StreamExplainQuery(req.gameId, req.topic);
            await foreach (var evt in mediator.CreateStream(query, ct))
            {
                // Serialize event as JSON
                var json = System.Text.Json.JsonSerializer.Serialize(evt);

                // Write SSE format: "data: {json}\n\n"
                await context.Response.WriteAsync($"data: {json}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }

            logger.LogInformation("Streaming explain completed for game {GameId}, topic: {Topic}", req.gameId, req.topic);
```

### REPLACE WITH:
```csharp
            // Set SSE headers
            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            try
            {
                var query = new StreamExplainQuery(req.gameId, req.topic);
                await foreach (var evt in mediator.CreateStream(query, ct))
                {
                    // Serialize event as JSON
                    var json = System.Text.Json.JsonSerializer.Serialize(evt);

                    // Write SSE format: "data: {json}\n\n"
                    await context.Response.WriteAsync($"data: {json}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }

                logger.LogInformation("Streaming explain completed for game {GameId}, topic: {Topic}", req.gameId, req.topic);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Streaming explain cancelled by client for game {GameId}, topic: {Topic}", req.gameId, req.topic);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Streaming generator boundary - must handle all errors gracefully without throwing
            // All expected exceptions are caught above; this ensures cleanup and error event on unexpected errors
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions for SSE streaming endpoint
                // Sends error event to client stream, specific exception handling in service layer
                logger.LogError(ex, "Error during streaming explain for game {GameId}, topic: {Topic}", req.gameId, req.topic);

                // Send error event if possible
                try
                {
                    var errorEvent = new RagStreamingEvent(
                        StreamingEventType.Error,
                        new StreamingError($"An error occurred: {ex.Message}", "INTERNAL_ERROR"),
                        DateTime.UtcNow);
                    var json = System.Text.Json.JsonSerializer.Serialize(errorEvent);
                    await context.Response.WriteAsync($"data: {json}\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                // Justification: Cleanup operation - must not throw during disposal/cleanup
                // Error event sending failure is logged but suppressed to ensure graceful stream termination
                catch
                {
                    // If we can't send error event, client connection is likely broken
                }
#pragma warning restore CA1031
            }
#pragma warning restore CA1031
```

---

## Fix #2: `/agents/qa/stream` (around line 387)

Look for similar pattern:
```csharp
            // ISSUE-1194: Error handling centralized in middleware + pipeline behavior
            // Set SSE headers
            context.Response.Headers["Content-Type"] = "text/event-stream";
            ...
            await foreach (var evt in mediator.CreateStream(query, ct))
```

Apply the SAME try-catch pattern as Fix #1, adapting the log messages to "QA" instead of "explain".

---

## Fix #3: `/agents/setup` (around line 561)

Same pattern again - wrap the `mediator.CreateStream()` loop in try-catch, adapt log messages to "setup guide".

---

## After Applying All Fixes

1. **Build**: `dotnet build`
2. **Test**: `dotnet test`
3. **Commit**: `git add . && git commit -m "hotfix: Restore SSE error handling for streaming endpoints"`
4. **Verify**: Test streaming endpoints work correctly

---

## Reference
Original implementation (before Issue #1194):
```bash
git show 998a513c^:apps/api/src/Api/Routing/AiEndpoints.cs | grep -A 80 "/agents/explain/stream"
```
