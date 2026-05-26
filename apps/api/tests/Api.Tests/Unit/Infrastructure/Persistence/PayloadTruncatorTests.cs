using System.Collections.Generic;
using System.Linq;
using Api.Infrastructure.Persistence;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Unit.Infrastructure.Persistence;

/// <summary>
/// Unit tests for PayloadTruncator — verifies collection trimming, oversize marker,
/// string-vs-collection discrimination, and threshold boundary (SP5 S1 T2b Q2).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class PayloadTruncatorTests
{
    [Fact]
    public void Truncate_LeavesSmallPayloadUnchanged()
    {
        var props = new Dictionary<string, object?> { ["Email"] = "a@x", ["Role"] = "admin" };
        var result = PayloadTruncator.Truncate(props, maxBytes: 256_000);
        result["Email"].Should().Be("a@x");
        result["Role"].Should().Be("admin");
        result.ContainsKey("_truncated").Should().BeFalse();
    }

    [Fact]
    public void Truncate_LargeCollectionField_TrimsToTenWithMetadata()
    {
        var games = Enumerable.Range(0, 543).Select(i => $"game-{i}").ToList();
        var props = new Dictionary<string, object?> { ["Email"] = "a@x", ["Games"] = games };

        var result = PayloadTruncator.Truncate(props, maxBytes: 256_000);

        var trimmedGames = result["Games"].Should().BeAssignableTo<IEnumerable<object>>().Subject;
        trimmedGames.Cast<object>().Should().HaveCount(10);
        result["_truncated"].Should().BeOfType<List<string>>();
        ((List<string>)result["_truncated"]!).Should().Contain("Games");
        var counts = result["_original_count"].Should().BeAssignableTo<IDictionary<string, int>>().Subject;
        counts["Games"].Should().Be(543);
    }

    [Fact]
    public void Truncate_StillOversize_ReturnsOversizeMarker()
    {
        var huge = new string('x', 300_000);
        var props = new Dictionary<string, object?> { ["Blob"] = huge };
        var result = PayloadTruncator.Truncate(props, maxBytes: 256_000);
        result.ContainsKey("_oversize").Should().BeTrue();
        ((bool)result["_oversize"]!).Should().BeTrue();
    }

    [Fact]
    public void Truncate_CollectionAtThreshold_NotTrimmed()
    {
        // exactly 50 items — NOT trimmed (threshold is > 50)
        var items = Enumerable.Range(0, 50).Select(i => $"item-{i}").ToList();
        var props = new Dictionary<string, object?> { ["Items"] = items };
        var result = PayloadTruncator.Truncate(props, maxBytes: 256_000);
        ((IEnumerable<object>)result["Items"]!).Cast<object>().Should().HaveCount(50);
        result.ContainsKey("_truncated").Should().BeFalse();
    }

    [Fact]
    public void Truncate_StringValue_NotTreatedAsCollection()
    {
        // a long-ish string is NOT a collection to trim (strings are IEnumerable<char>)
        var props = new Dictionary<string, object?> { ["Name"] = "a-fairly-long-name-but-still-scalar" };
        var result = PayloadTruncator.Truncate(props, maxBytes: 256_000);
        result["Name"].Should().Be("a-fairly-long-name-but-still-scalar");
        result.ContainsKey("_truncated").Should().BeFalse();
    }
}
