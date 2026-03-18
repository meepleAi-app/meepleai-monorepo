using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "DatabaseSync")]
public class SchemaDiffEngineTests
{
    [Fact]
    public void ComputeDiff_WithIdenticalLists_ReturnsAllCommon()
    {
        var local = new List<MigrationInfo>
        {
            new("20260316_Beta0", "9.0.0", DateTime.UtcNow),
            new("20260317_AddFts", "9.0.0", DateTime.UtcNow)
        };
        var staging = new List<MigrationInfo>
        {
            new("20260316_Beta0", "9.0.0", DateTime.UtcNow),
            new("20260317_AddFts", "9.0.0", DateTime.UtcNow)
        };
        var result = SchemaDiffEngine.ComputeDiff(local, staging);
        Assert.Equal(2, result.Common.Count);
        Assert.Empty(result.LocalOnly);
        Assert.Empty(result.StagingOnly);
    }

    [Fact]
    public void ComputeDiff_WithLocalOnlyMigrations_ReturnsCorrectly()
    {
        var local = new List<MigrationInfo>
        {
            new("20260316_Beta0", "9.0.0", DateTime.UtcNow),
            new("20260318_NewFeature", "9.0.0", DateTime.UtcNow)
        };
        var staging = new List<MigrationInfo> { new("20260316_Beta0", "9.0.0", DateTime.UtcNow) };
        var result = SchemaDiffEngine.ComputeDiff(local, staging);
        Assert.Single(result.Common);
        Assert.Single(result.LocalOnly);
        Assert.Equal("20260318_NewFeature", result.LocalOnly[0].MigrationId);
        Assert.Empty(result.StagingOnly);
    }

    [Fact]
    public void ComputeDiff_WithStagingOnlyMigrations_ReturnsCorrectly()
    {
        var local = new List<MigrationInfo> { new("20260316_Beta0", "9.0.0", DateTime.UtcNow) };
        var staging = new List<MigrationInfo>
        {
            new("20260316_Beta0", "9.0.0", DateTime.UtcNow),
            new("20260317_StagingFix", "9.0.0", DateTime.UtcNow)
        };
        var result = SchemaDiffEngine.ComputeDiff(local, staging);
        Assert.Single(result.Common);
        Assert.Empty(result.LocalOnly);
        Assert.Single(result.StagingOnly);
    }

    [Fact]
    public void ComputeDiff_WithEmptyLists_ReturnsAllEmpty()
    {
        var result = SchemaDiffEngine.ComputeDiff(new List<MigrationInfo>(), new List<MigrationInfo>());
        Assert.Empty(result.Common);
        Assert.Empty(result.LocalOnly);
        Assert.Empty(result.StagingOnly);
    }
}
