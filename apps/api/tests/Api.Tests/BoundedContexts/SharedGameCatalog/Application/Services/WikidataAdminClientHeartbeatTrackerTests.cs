using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #2470 — TDD tests for <see cref="WikidataAdminClientHeartbeatTracker"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2470")]
public class WikidataAdminClientHeartbeatTrackerTests
{
    private static readonly ILogger<WikidataAdminClientHeartbeatTracker> NullLogger =
        NullLogger<WikidataAdminClientHeartbeatTracker>.Instance;

    private static readonly DateTime BaseUtc = new(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetConnectedCount_EmptyTracker_ReturnsZero()
    {
        var sut = new WikidataAdminClientHeartbeatTracker(NullLogger);

        sut.GetConnectedCount(BaseUtc).Should().Be(0);
    }

    [Fact]
    public void RecordHeartbeat_SingleUser_GaugeReports1()
    {
        var sut = new WikidataAdminClientHeartbeatTracker(NullLogger);

        sut.RecordHeartbeat(Guid.NewGuid(), BaseUtc);

        sut.GetConnectedCount(BaseUtc).Should().Be(1);
    }

    [Fact]
    public void RecordHeartbeat_TwoDistinctUsers_GaugeReports2()
    {
        var sut = new WikidataAdminClientHeartbeatTracker(NullLogger);

        sut.RecordHeartbeat(Guid.NewGuid(), BaseUtc);
        sut.RecordHeartbeat(Guid.NewGuid(), BaseUtc);

        sut.GetConnectedCount(BaseUtc).Should().Be(2);
    }

    [Fact]
    public void RecordHeartbeat_SameUserTwice_StillReports1()
    {
        var sut = new WikidataAdminClientHeartbeatTracker(NullLogger);
        var userId = Guid.NewGuid();

        sut.RecordHeartbeat(userId, BaseUtc);
        sut.RecordHeartbeat(userId, BaseUtc.AddSeconds(30));

        sut.GetConnectedCount(BaseUtc.AddSeconds(30)).Should().Be(1);
    }

    [Fact]
    public void RecordHeartbeat_EmptyGuid_IsIgnored()
    {
        var sut = new WikidataAdminClientHeartbeatTracker(NullLogger);

        sut.RecordHeartbeat(Guid.Empty, BaseUtc);

        sut.GetConnectedCount(BaseUtc)
            .Should().Be(0, "empty Guid is non-sensical and MUST NOT pollute the tracker");
    }

    [Fact]
    public void GetConnectedCount_ExpiredEntries_AreEvicted()
    {
        var sut = new WikidataAdminClientHeartbeatTracker(NullLogger);
        var oldUser = Guid.NewGuid();
        var freshUser = Guid.NewGuid();

        sut.RecordHeartbeat(oldUser, BaseUtc);
        // 91s later — past the 90s TTL — old user has not heartbeated.
        sut.RecordHeartbeat(freshUser, BaseUtc.AddSeconds(91));

        sut.GetConnectedCount(BaseUtc.AddSeconds(91))
            .Should().Be(1, "the 91s-old entry MUST be evicted on the next read");
    }

    [Fact]
    public void GetConnectedCount_RefreshedEntry_StaysConnected()
    {
        var sut = new WikidataAdminClientHeartbeatTracker(NullLogger);
        var userId = Guid.NewGuid();

        sut.RecordHeartbeat(userId, BaseUtc);
        // 80s later (within TTL) — refresh.
        sut.RecordHeartbeat(userId, BaseUtc.AddSeconds(80));
        // Another 80s later — would expire from original, but refresh
        // kept it alive.
        sut.GetConnectedCount(BaseUtc.AddSeconds(160))
            .Should().Be(1, "an active user that refreshes within TTL stays connected indefinitely");
    }

    [Fact]
    public void GetConnectedCount_BoundaryAtTtl_IsTreatedAsConnected()
    {
        // The TTL boundary is "strictly less than threshold". An entry at
        // exactly TTL seconds old is still connected. This keeps a slow
        // ping that arrives at exactly the boundary from flipping the gauge.
        var sut = new WikidataAdminClientHeartbeatTracker(NullLogger);
        var userId = Guid.NewGuid();

        sut.RecordHeartbeat(userId, BaseUtc);

        sut.GetConnectedCount(BaseUtc.AddSeconds(WikidataAdminClientHeartbeatTracker.TtlSeconds))
            .Should().Be(1);
    }

    [Fact]
    public void RecordHeartbeat_1000ConcurrentCallers_AllRecorded()
    {
        var sut = new WikidataAdminClientHeartbeatTracker(NullLogger);
        var userIds = Enumerable.Range(0, 1000).Select(_ => Guid.NewGuid()).ToArray();

        Parallel.ForEach(userIds, id => sut.RecordHeartbeat(id, BaseUtc));

        sut.GetConnectedCount(BaseUtc).Should().Be(1000);
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new WikidataAdminClientHeartbeatTracker(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
