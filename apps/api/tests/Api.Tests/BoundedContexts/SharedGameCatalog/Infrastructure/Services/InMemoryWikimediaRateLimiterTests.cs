using System.Diagnostics;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class InMemoryWikimediaRateLimiterTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Default 5 RPS behaviour (production configuration)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AcquireAsync_FirstFiveTokens_AreImmediate()
    {
        using var sut = new InMemoryWikimediaRateLimiter();
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < 5; i++)
        {
            await sut.AcquireAsync();
        }

        stopwatch.Stop();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200,
            "the first 5 tokens are pre-loaded in the bucket and should not block");
    }

    [Fact]
    public async Task AcquireAsync_SixthToken_WaitsForReplenishment()
    {
        // Use a tighter test bucket (2 RPS, 500ms replenishment) so the wait is
        // observable without slowing the suite.
        using var sut = new InMemoryWikimediaRateLimiter(
            tokenLimit: 2,
            tokensPerPeriod: 2,
            replenishmentPeriod: TimeSpan.FromMilliseconds(500));

        // Drain bucket — 2 immediate acquisitions.
        await sut.AcquireAsync();
        await sut.AcquireAsync();

        // 3rd acquisition must wait for the next replenishment tick.
        var stopwatch = Stopwatch.StartNew();
        await sut.AcquireAsync();
        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(300,
            "after draining the bucket the next token waits for replenishment ~500ms");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Cancellation respect
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AcquireAsync_CancellationToken_PropagatesWhenFired()
    {
        using var sut = new InMemoryWikimediaRateLimiter(
            tokenLimit: 1,
            tokensPerPeriod: 1,
            replenishmentPeriod: TimeSpan.FromSeconds(60));

        await sut.AcquireAsync(); // drain

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var act = async () => await sut.AcquireAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>(
            "callers must be able to abort a long wait via the cancellation token");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Disposal — defensive failure mode
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AcquireAsync_AfterDispose_ThrowsObjectDisposed()
    {
        var sut = new InMemoryWikimediaRateLimiter();
        sut.Dispose();

        var act = async () => await sut.AcquireAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Concurrency
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AcquireAsync_ParallelCallers_AllComplete()
    {
        using var sut = new InMemoryWikimediaRateLimiter(
            tokenLimit: 3,
            tokensPerPeriod: 3,
            replenishmentPeriod: TimeSpan.FromMilliseconds(200));

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.AcquireAsync().AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        tasks.Should().AllSatisfy(t =>
            t.IsCompletedSuccessfully.Should().BeTrue(
                "all parallel acquisitions complete without exception"));
    }
}
