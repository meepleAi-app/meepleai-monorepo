// apps/api/tests/Api.Tests/Unit/DocumentProcessing/IndexerVersionRegistryTests.cs
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "1673")]
public sealed class IndexerVersionRegistryTests
{
    [Fact]
    public void Current_ReturnsLatestSelectableVersion()
    {
        // SP-E #3409 (epic #3403): Current bumped to the coordinate-aware pipeline version so a
        // corpus re-extract re-populates bounding_boxes_json for region grounding.
        IndexerVersionRegistry.Current.Version.Should().Be("v1.2");
        IndexerVersionRegistry.Current.IsSelectable.Should().BeTrue();
        IndexerVersionRegistry.Current.DisplayName.Should().Contain("coordinate-aware");
    }

    [Fact]
    public void Legacy_ReturnsV0NonSelectable()
    {
        IndexerVersionRegistry.Legacy.Version.Should().Be("v0");
        IndexerVersionRegistry.Legacy.IsSelectable.Should().BeFalse();
    }

    [Fact]
    public void All_ContainsLegacyV0AndV1_0AndV1_1AndV1_2()
    {
        var versions = IndexerVersionRegistry.All;
        versions.Should().HaveCountGreaterThanOrEqualTo(4);
        versions.Should().Contain(v => v.Version == "v0");
        // v1.0 / v1.1 retained per the ≥18mo deprecation policy even though they are no longer Current.
        versions.Should().Contain(v => v.Version == "v1.0");
        versions.Should().Contain(v => v.Version == "v1.1");
        versions.Should().Contain(v => v.Version == "v1.2");
    }

    [Theory]
    [InlineData("v0")]
    [InlineData("v1.0")]
    [InlineData("v1.1")]
    [InlineData("v1.2")]
    public void TryGet_KnownVersion_ReturnsTrue(string input)
    {
        IndexerVersionRegistry.TryGet(input, out var version).Should().BeTrue();
        version!.Version.Should().Be(input);
    }

    [Theory]
    [InlineData("v99")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGet_UnknownVersion_ReturnsFalse(string? input)
    {
        IndexerVersionRegistry.TryGet(input, out var version).Should().BeFalse();
        version.Should().BeNull();
    }

    [Fact]
    public void IsSelectable_LegacyV0_ReturnsFalse()
    {
        IndexerVersionRegistry.IsSelectable("v0").Should().BeFalse();
    }

    [Fact]
    public void IsSelectable_Current_ReturnsTrue()
    {
        // The retained v1.0/v1.1 and the new coordinate-aware v1.2 all remain selectable from /reindex.
        IndexerVersionRegistry.IsSelectable("v1.0").Should().BeTrue();
        IndexerVersionRegistry.IsSelectable("v1.1").Should().BeTrue();
        IndexerVersionRegistry.IsSelectable("v1.2").Should().BeTrue();
    }

    [Fact]
    public void IsSelectable_Unknown_ReturnsFalse()
    {
        IndexerVersionRegistry.IsSelectable("v99").Should().BeFalse();
    }
}
