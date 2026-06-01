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
        IndexerVersionRegistry.Current.Version.Should().Be("v1.0");
        IndexerVersionRegistry.Current.IsSelectable.Should().BeTrue();
    }

    [Fact]
    public void Legacy_ReturnsV0NonSelectable()
    {
        IndexerVersionRegistry.Legacy.Version.Should().Be("v0");
        IndexerVersionRegistry.Legacy.IsSelectable.Should().BeFalse();
    }

    [Fact]
    public void All_ContainsLegacyAndCurrent()
    {
        var versions = IndexerVersionRegistry.All;
        versions.Should().HaveCountGreaterThanOrEqualTo(2);
        versions.Should().Contain(v => v.Version == "v0");
        versions.Should().Contain(v => v.Version == "v1.0");
    }

    [Theory]
    [InlineData("v0")]
    [InlineData("v1.0")]
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
        IndexerVersionRegistry.IsSelectable("v1.0").Should().BeTrue();
    }

    [Fact]
    public void IsSelectable_Unknown_ReturnsFalse()
    {
        IndexerVersionRegistry.IsSelectable("v99").Should().BeFalse();
    }
}
