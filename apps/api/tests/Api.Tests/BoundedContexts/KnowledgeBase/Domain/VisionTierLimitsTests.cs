using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for VisionTierLimits static lookup.
/// Session Vision AI feature.
/// </summary>
public class VisionTierLimitsTests
{
    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void AlphaTier_HasExpectedLimits()
    {
        var config = VisionTierLimits.GetConfig("alpha");

        config.MaxImagesPerMessage.Should().Be(5);
        config.MaxSnapshotsPerSession.Should().Be(20);
        config.GameStateExtractionEnabled.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void FreeTier_HasExpectedLimits()
    {
        var config = VisionTierLimits.GetConfig("free");

        config.MaxImagesPerMessage.Should().Be(2);
        config.MaxSnapshotsPerSession.Should().Be(5);
        config.GameStateExtractionEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void NullTier_ReturnsFreeDefaults()
    {
        var config = VisionTierLimits.GetConfig(null);

        config.MaxImagesPerMessage.Should().Be(2);
        config.MaxSnapshotsPerSession.Should().Be(5);
        config.GameStateExtractionEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void UnknownTier_ReturnsFreeDefaults()
    {
        var config = VisionTierLimits.GetConfig("nonexistent-tier");

        config.MaxImagesPerMessage.Should().Be(2);
        config.MaxSnapshotsPerSession.Should().Be(5);
        config.GameStateExtractionEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    public void TierLookup_IsCaseInsensitive()
    {
        var lower = VisionTierLimits.GetConfig("alpha");
        var upper = VisionTierLimits.GetConfig("ALPHA");
        var mixed = VisionTierLimits.GetConfig("Alpha");

        lower.Should().Be(upper);
        lower.Should().Be(mixed);
    }
}
