using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Services;

[Trait("Category", TestCategories.Unit)]
public sealed class StatusBannerMessageIdTests
{
    [Fact]
    public void Compute_SameInputs_ReturnsSameGuid()
    {
        var ts = new DateTime(2026, 5, 17, 10, 0, 0, DateTimeKind.Utc);

        var a = StatusBannerMessageId.Compute("hello", BannerSeverity.Warning, ts);
        var b = StatusBannerMessageId.Compute("hello", BannerSeverity.Warning, ts);

        a.Should().Be(b);
        a.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Compute_DifferentMessage_ReturnsDifferentGuid()
    {
        var ts = new DateTime(2026, 5, 17, 10, 0, 0, DateTimeKind.Utc);

        var a = StatusBannerMessageId.Compute("hello", BannerSeverity.Info, ts);
        var b = StatusBannerMessageId.Compute("world", BannerSeverity.Info, ts);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Compute_DifferentSeverity_ReturnsDifferentGuid()
    {
        var ts = new DateTime(2026, 5, 17, 10, 0, 0, DateTimeKind.Utc);

        var a = StatusBannerMessageId.Compute("x", BannerSeverity.Info, ts);
        var b = StatusBannerMessageId.Compute("x", BannerSeverity.Critical, ts);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Compute_DifferentUpdatedAt_ReturnsDifferentGuid()
    {
        var t1 = new DateTime(2026, 5, 17, 10, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddSeconds(1);

        var a = StatusBannerMessageId.Compute("x", BannerSeverity.Info, t1);
        var b = StatusBannerMessageId.Compute("x", BannerSeverity.Info, t2);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Compute_NormalizesToUtc()
    {
        var utc = new DateTime(2026, 5, 17, 10, 0, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime(); // Kind=Local

        var fromUtc = StatusBannerMessageId.Compute("x", BannerSeverity.Info, utc);
        var fromLocal = StatusBannerMessageId.Compute("x", BannerSeverity.Info, local);

        fromUtc.Should().Be(fromLocal);
    }
}
