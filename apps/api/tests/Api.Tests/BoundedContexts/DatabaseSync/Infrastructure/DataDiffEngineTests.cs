using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Xunit;

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
        Assert.Equal(2, identical);
        Assert.Empty(modified);
        Assert.Empty(localOnly);
        Assert.Empty(stagingOnly);
    }

    [Fact]
    public void ComputeHashDiff_DifferentHashes_DetectsModified()
    {
        var local = new Dictionary<string, string> { ["id1"] = "hashA" };
        var staging = new Dictionary<string, string> { ["id1"] = "hashB" };
        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(local, staging);
        Assert.Equal(0, identical);
        Assert.Single(modified);
        Assert.Equal("id1", modified[0]);
    }

    [Fact]
    public void ComputeHashDiff_MissingRows_DetectsOnlyLocal_OnlyStaging()
    {
        var local = new Dictionary<string, string> { ["id1"] = "hash1", ["id3"] = "hash3" };
        var staging = new Dictionary<string, string> { ["id1"] = "hash1", ["id2"] = "hash2" };
        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(local, staging);
        Assert.Equal(1, identical);
        Assert.Empty(modified);
        Assert.Single(localOnly);
        Assert.Equal("id3", localOnly[0]);
        Assert.Single(stagingOnly);
        Assert.Equal("id2", stagingOnly[0]);
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
        Assert.Equal(3, safe.Count);
        Assert.Contains("id", safe);
        Assert.Contains("name", safe);
        Assert.Contains("age", safe);
        Assert.DoesNotContain("embedding", safe);
        Assert.DoesNotContain("data", safe);
        Assert.DoesNotContain("config", safe);
    }

    [Fact]
    public void ComputeHashDiff_EmptyMaps_ReturnsZeroes()
    {
        var (identical, modified, localOnly, stagingOnly) = DataDiffEngine.ComputeHashDiff(
            new Dictionary<string, string>(), new Dictionary<string, string>());
        Assert.Equal(0, identical);
        Assert.Empty(modified);
        Assert.Empty(localOnly);
        Assert.Empty(stagingOnly);
    }
}
