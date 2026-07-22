using System.Text;

namespace Api.Routing.Sse;

/// <summary>
/// Serializes writes to a single Server-Sent Events response body (#3263).
///
/// A live SSE stream has two concurrent writers — a keep-alive heartbeat task and
/// the main event loop — that both write to the same <see cref="HttpResponse"/>.
/// Kestrel forbids concurrent response-body writes ("Concurrent writes to the
/// response body are not supported") and interleaving corrupts SSE frames, so
/// every writer on a connection MUST go through the same gate.
/// </summary>
internal static class SseFrameWriter
{
    /// <summary>
    /// Writes a complete SSE frame (including its trailing blank line) and flushes,
    /// holding <paramref name="gate"/> so no other frame on the same connection
    /// interleaves. Callers share one <see cref="SemaphoreSlim"/> per connection.
    /// </summary>
    public static async Task WriteFrameAsync(
        HttpResponse response,
        SemaphoreSlim gate,
        string frame,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(frame);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await response.Body.WriteAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
            await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
