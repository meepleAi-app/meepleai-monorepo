using Api.Configuration;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
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
/// invalidation for callers that need the failure to be VISIBLE:
/// </para>
/// <list type="bullet">
/// <item>
/// the domain-event handlers that derive from <c>DomainEventHandlerBase</c> (it logs and rethrows) and use this
/// wrapper: <c>ConfigurationUpdatedEventHandler</c> with its Created, Deleted and Toggled siblings, and
/// <c>RateLimitConfigUpdatedEventHandler</c>. In staging and production (<c>DomainEventOutbox:Mode</c> =
/// <c>OutboxOnly</c>) the outbox processor retries a handler that throws, up to <c>MaxAttempts</c>; swallowing
/// the error marks the row sent and leaves the cache stale until it expires (#4659). The
/// <c>PdfStateChanged…</c> and <c>PdfMetadataChanged…</c> invalidation handlers also call it unprotected;
/// </item>
/// <item>
/// the <c>KbFlagDriftAuditJob</c>, whose retry policy only reacts to a thrown error;
/// </item>
/// <item>
/// the admin endpoints whose whole purpose is to invalidate (<c>InvalidateCacheCommandHandler</c>,
/// <c>InvalidateCacheByTagCommandHandler</c>, <c>InvalidateGameCacheCommandHandler</c>): they must not
/// answer success when nothing was invalidated.
/// </item>
/// </list>
/// <para>
/// (The toolkit and agent-definition handlers do NOT depend on this wrapper: they use the raw
/// <c>HybridCache</c> with their own retry policy.)
/// </para>
/// <para>
/// A command handler that has ALREADY committed a write, and so must not turn into an error because a cache
/// entry could not be dropped, protects ITS OWN call (#4720: <c>RecordGameSessionCommandHandler</c>,
/// <c>CreateMechanicGoldenClaimHandler</c>). Making the wrapper swallow would fix those and silently break
/// the callers above, so a test fails if someone aligns it.
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

    /// <summary>
    /// The premise of everything else in this class and of the try/catch in the two command handlers: the
    /// real <c>Microsoft.Extensions.Caching.Hybrid</c> THROWS on removal when its second level is down. If a
    /// library update makes it tolerant, this is the test that fails first, with the reason stated, and the
    /// wrapper tests below (and the handlers' catch) can be reconsidered instead of failing for a reason that
    /// looks like somebody changed the wrapper.
    /// </summary>
    [Fact]
    public async Task TheLibrary_WhenItsL2IsDown_ThrowsOnRemoval_WhichIsThePremiseOfTheOtherTests()
    {
        var cache = HybridCacheWithL2Down.Create();

        var remove = () => cache.RemoveAsync("some-key", TestContext.Current.CancellationToken).AsTask();
        var removeByTag = () => cache.RemoveByTagAsync("some-tag", TestContext.Current.CancellationToken).AsTask();

        await remove.Should().ThrowAsync<InvalidOperationException>(
            "HybridCache.RemoveAsync throws when its L2 is down; if this started passing the library became tolerant");
        await removeByTag.Should().ThrowAsync<InvalidOperationException>(
            "HybridCache.RemoveByTagAsync throws when its L2 is down; if this started passing the library became tolerant");
    }

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
