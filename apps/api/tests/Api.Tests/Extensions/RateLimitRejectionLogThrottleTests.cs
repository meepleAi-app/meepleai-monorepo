using Api.Extensions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Extensions;

/// <summary>
/// #4912 — the warning a rate-limit rejection writes is itself bounded: one per bucket per
/// <see cref="RateLimitingServiceExtensions.RejectionLogInterval"/>. The rejection path is not rate
/// limited, so without this a flood against any limited route would be a flood of warnings — the
/// thing the limiter protects would cost more per request than before.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class RateLimitRejectionLogThrottleTests
{
    private static readonly long Interval = (long)RateLimitingServiceExtensions.RejectionLogInterval.TotalMilliseconds;

    [Fact]
    public void ABucket_IsLoggedOnce_PerInterval()
    {
        var bucket = $"SessionCodeRead|{Guid.NewGuid():N}|10.0.0.1";

        RateLimitingServiceExtensions.ShouldLogRejection(bucket, 0).Should().BeTrue("the first rejection is news");
        RateLimitingServiceExtensions.ShouldLogRejection(bucket, Interval / 2).Should().BeFalse("same bucket, same window");
        RateLimitingServiceExtensions.ShouldLogRejection(bucket, Interval - 1).Should().BeFalse("still inside the window");
        RateLimitingServiceExtensions.ShouldLogRejection(bucket, Interval).Should().BeTrue("a new window is news again");
        RateLimitingServiceExtensions.ShouldLogRejection(bucket, Interval + 1).Should().BeFalse();
    }

    [Fact]
    public void DifferentBuckets_DoNotShareTheWindow()
    {
        var a = $"SessionCodeRead|{Guid.NewGuid():N}|10.0.0.1";
        var b = $"SessionCodeRead|{Guid.NewGuid():N}|10.0.0.1";

        RateLimitingServiceExtensions.ShouldLogRejection(a, 0).Should().BeTrue();
        RateLimitingServiceExtensions.ShouldLogRejection(b, 0).Should().BeTrue("another user is another bucket");
    }
}
