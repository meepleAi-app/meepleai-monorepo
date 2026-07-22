using Api.Routing.Sse;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Api.Tests.Routing.Sse;

/// <summary>
/// #3263: the SSE endpoints' heartbeat task and main event loop both write to the
/// same response body. SseFrameWriter must serialize those writes so they never
/// overlap (Kestrel forbids concurrent response-body writes).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SseFrameWriterTests
{
    /// <summary>
    /// A response-body stream that records the maximum number of writes observed
    /// in flight at the same time. Each write holds the "in flight" state across an
    /// await, so unsynchronized concurrent callers push the observed maximum above 1.
    /// </summary>
    private sealed class ConcurrencyTrackingStream : Stream
    {
        private int _current;
        private int _max;

        public int MaxConcurrent => Volatile.Read(ref _max);

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var inFlight = Interlocked.Increment(ref _current);
            int observed;
            while (inFlight > (observed = Volatile.Read(ref _max)))
            {
                Interlocked.CompareExchange(ref _max, inFlight, observed);
            }

            try
            {
                // Hold the "in flight" window open so a concurrent write would overlap.
                await Task.Delay(5, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref _current);
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Flush() { }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set { } }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task WriteFrameAsync_ConcurrentCallers_NeverOverlapOnTheResponseBody()
    {
        var tracker = new ConcurrencyTrackingStream();
        var context = new DefaultHttpContext();
        context.Response.Body = tracker;
        using var gate = new SemaphoreSlim(1, 1);

        // Fire many writers concurrently, exactly like the heartbeat task racing the
        // main event loop.
        var writers = Enumerable.Range(0, 25)
            .Select(i => SseFrameWriter.WriteFrameAsync(
                context.Response, gate, $"data: {i}\n\n", CancellationToken.None))
            .ToArray();

        await Task.WhenAll(writers);

        tracker.MaxConcurrent.Should().Be(1, "writes to a single SSE response body must be serialized");
    }
}
