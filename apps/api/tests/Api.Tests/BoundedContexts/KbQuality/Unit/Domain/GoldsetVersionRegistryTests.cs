using Api.BoundedContexts.KbQuality.Domain.Goldset;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KbQuality")]
public sealed class GoldsetVersionRegistryTests
{
    [Fact]
    public void Registry_ContainsAutoCurrent()
    {
        GoldsetVersion.Registry.Should().ContainSingle(v => v.Version == "auto-v1");
        GoldsetVersion.AutoCurrent.Version.Should().Be("auto-v1");
        GoldsetVersion.AutoCurrent.Strategy.Should().Be(GoldsetStrategy.LlmAutoGen);
    }

    [Fact]
    public void TryGet_KnownVersion_ReturnsTrue()
    {
        var found = GoldsetVersion.TryGet("auto-v1", out var version);

        found.Should().BeTrue();
        version!.DisplayName.Should().Be("Auto LLM v1");
    }

    [Fact]
    public void TryGet_UnknownVersion_ReturnsFalse()
    {
        var found = GoldsetVersion.TryGet("manual-v1", out var version);

        found.Should().BeFalse();
        version.Should().BeNull();
    }

    [Fact]
    public void TryGet_NullOrWhitespace_ReturnsFalse()
    {
        GoldsetVersion.TryGet(null, out _).Should().BeFalse();
        GoldsetVersion.TryGet("", out _).Should().BeFalse();
        GoldsetVersion.TryGet("   ", out _).Should().BeFalse();
    }
}
