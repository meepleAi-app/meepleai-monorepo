using Api.Configuration;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// What <see cref="HybridCacheService"/> does when its Redis level is down.
/// </summary>
/// <remarks>
/// <para>
/// <b>It throws, on purpose, and this is a contract worth pinning.</b> The removal methods are the
/// invalidation for three kinds of caller that need the failure to be VISIBLE:
/// the domain-event handlers that run from the outbox in staging and production (the outbox retries a
/// handler that throws, up to <c>MaxAttempts</c>; swallowing the error marks the row sent and leaves the
/// cache stale until it expires, #4659), the <c>KbFlagDriftAuditJob</c> (its retry policy only reacts to a
/// thrown error), and the admin endpoints whose whole purpose is to invalidate (they must not answer
/// success when nothing was invalidated).
/// </para>
/// <para>
/// A command handler that has ALREADY committed a write, and so must not turn into an error because a cache
/// entry could not be dropped, protects ITS OWN call (#4720: <c>RecordGameSessionCommandHandler</c>,
/// <c>CreateMechanicGoldenClaimHandler</c>). Making the wrapper swallow would fix those and silently break
/// the three above, so a test fails if someone aligns it.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
public sealed class HybridCacheServiceBackendDownTests
{
    private static HybridCacheService WrapperOverAnL2ThatIsDown() =>
        new(
            HybridCacheWithL2Down.Create(),
            Options.Create(new HybridCacheConfiguration()),
            NullLogger<HybridCacheService>.Instance);

    [Fact]
    public async Task RemoveAsync_WhenTheBackendIsDown_StillThrows_SoACallerThatNeedsToKnowCan()
    {
        var wrapper = WrapperOverAnL2ThatIsDown();

        var act = () => wrapper.RemoveAsync("some-key", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RemoveByTagAsync_WhenTheBackendIsDown_StillThrows_SoACallerThatNeedsToKnowCan()
    {
        var wrapper = WrapperOverAnL2ThatIsDown();

        var act = () => wrapper.RemoveByTagAsync("some-tag", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RemoveByTagAcrossReplicasAsync_WhenTheBackendIsDown_StillThrows_SoTheOutboxRetriesTheEvent()
    {
        var wrapper = WrapperOverAnL2ThatIsDown();

        var act = () => wrapper.RemoveByTagAcrossReplicasAsync("some-tag", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
