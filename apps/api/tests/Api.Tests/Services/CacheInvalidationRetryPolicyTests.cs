using System.Diagnostics;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CacheInvalidationRetryPolicy"/>.
/// Issue #613: SharedGameCatalog cache invalidation resilience.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "613")]
public sealed class CacheInvalidationRetryPolicyTests
{
    private static CacheInvalidationRetryPolicy CreatePolicy() =>
        new(NullLogger<CacheInvalidationRetryPolicy>.Instance);

    [Fact]
    public async Task ExecuteAsync_WhenOperationSucceeds_RunsExactlyOnce()
    {
        // Arrange
        var policy = CreatePolicy();
        var calls = 0;

        // Act
        await policy.ExecuteAsync(
            ct => { calls++; return ValueTask.CompletedTask; },
            "test.success",
            CancellationToken.None);

        // Assert
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTransientFailure_RetriesAndEventuallySucceeds()
    {
        // Arrange
        var policy = CreatePolicy();
        var calls = 0;

        // Act
        await policy.ExecuteAsync(
            ct =>
            {
                calls++;
                if (calls < 3)
                {
                    throw new InvalidOperationException("transient");
                }
                return ValueTask.CompletedTask;
            },
            "test.transient",
            CancellationToken.None);

        // Assert: 1 initial + 2 retries = 3 calls
        calls.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlwaysFailing_ThrowsAfter4TotalAttempts()
    {
        // Arrange
        var policy = CreatePolicy();
        var calls = 0;

        // Act
        var act = () => policy.ExecuteAsync(
            ct =>
            {
                calls++;
                throw new InvalidOperationException("permanent");
            },
            "test.permanent",
            CancellationToken.None);

        // Assert: 1 initial + 3 retries = 4 attempts
        await act.Should().ThrowAsync<InvalidOperationException>();
        calls.Should().Be(4);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlwaysFailing_StaysUnderFourSecondsBudget()
    {
        // Arrange
        var policy = CreatePolicy();
        var sw = Stopwatch.StartNew();

        // Act
        try
        {
            await policy.ExecuteAsync(
                ct => throw new InvalidOperationException("permanent"),
                "test.budget",
                CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
            // expected
        }

        sw.Stop();

        // Assert: 200 + 400 + 800 = 1400ms base + jitter (≤50%) ≈ ~2.1s worst case.
        // Budget: 4000ms with margin for slow CI runners.
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArgumentException_DoesNotRetry()
    {
        // Arrange — ArgumentException represents a programming bug, not transient infra failure.
        var policy = CreatePolicy();
        var calls = 0;

        // Act
        var act = () => policy.ExecuteAsync(
            ct =>
            {
                calls++;
                throw new ArgumentException("bug");
            },
            "test.arg",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenObjectDisposedException_DoesNotRetry()
    {
        // Arrange — disposed dependency cannot recover via retry.
        var policy = CreatePolicy();
        var calls = 0;

        // Act
        var act = () => policy.ExecuteAsync(
            ct =>
            {
                calls++;
                throw new ObjectDisposedException("HybridCache");
            },
            "test.disposed",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ObjectDisposedException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancels_PropagatesCancellation()
    {
        // Arrange
        var policy = CreatePolicy();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var act = () => policy.ExecuteAsync(
            ct => { ct.ThrowIfCancellationRequested(); return ValueTask.CompletedTask; },
            "test.cancel",
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationNameIsEmpty_ThrowsArgumentException()
    {
        var policy = CreatePolicy();
        var act = () => policy.ExecuteAsync(
            ct => ValueTask.CompletedTask,
            string.Empty,
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationIsNull_ThrowsArgumentNullException()
    {
        var policy = CreatePolicy();
        var act = () => policy.ExecuteAsync(null!, "x", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
