using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.DatabaseSync.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DatabaseSync")]
public class DataDiffEngineTests
{
    [Fact]
    public void ComputeHashDiff_IdenticalHashes_ReturnsOnlyIdentical()
    {
        var local = new Dictionary<string, string> { ["id1"] = "hash1", ["id2"] = "hash2" };
        var staging = new Dictionary<string, string> { ["id1"] = "hash1", ["id2"] = "hash2" };
        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(local, staging);
        identical.Should().Be(2);
        modified.Should().BeEmpty();
        localOnly.Should().BeEmpty();
        stagingOnly.Should().BeEmpty();
    }

    [Fact]
    public void ComputeHashDiff_DifferentHashes_DetectsModified()
    {
        var local = new Dictionary<string, string> { ["id1"] = "hashA" };
        var staging = new Dictionary<string, string> { ["id1"] = "hashB" };
        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(local, staging);
        identical.Should().Be(0);
        modified.Should().ContainSingle();
        modified[0].Should().Be("id1");
    }

    [Fact]
    public void ComputeHashDiff_MissingRows_DetectsOnlyLocal_OnlyStaging()
    {
        var local = new Dictionary<string, string> { ["id1"] = "hash1", ["id3"] = "hash3" };
        var staging = new Dictionary<string, string> { ["id1"] = "hash1", ["id2"] = "hash2" };
        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(local, staging);
        identical.Should().Be(1);
        modified.Should().BeEmpty();
        localOnly.Should().ContainSingle();
        localOnly[0].Should().Be("id3");
        stagingOnly.Should().ContainSingle();
        stagingOnly[0].Should().Be("id2");
    }

    [Fact]
    public void FilterSafeColumns_ExcludesUnsafeTypes()
    {
        var columns = new List<(string name, string type)>
        {
            ("id", "uuid"), ("name", "text"), ("embedding", "vector"),
            ("data", "bytea"), ("config", "jsonb"), ("age", "int4")
        };
        var safe = DataDiffEngine.FilterSafeColumns(columns);
        safe.Count.Should().Be(3);
        safe.Should().Contain("id");
        safe.Should().Contain("name");
        safe.Should().Contain("age");
        safe.Should().NotContain("embedding");
        safe.Should().NotContain("data");
        safe.Should().NotContain("config");
    }

    [Fact]
    public void ComputeHashDiff_EmptyMaps_ReturnsZeroes()
    {
        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(
            new Dictionary<string, string>(), new Dictionary<string, string>());
        identical.Should().Be(0);
        modified.Should().BeEmpty();
        localOnly.Should().BeEmpty();
        stagingOnly.Should().BeEmpty();
    }
}
