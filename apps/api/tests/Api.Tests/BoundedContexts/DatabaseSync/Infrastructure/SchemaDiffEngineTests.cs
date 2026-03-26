using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Xunit;
using FluentAssertions;

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
        result.Common.Count.Should().Be(2);
        result.LocalOnly.Should().BeEmpty();
        result.StagingOnly.Should().BeEmpty();
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
        result.Common.Should().ContainSingle();
        result.LocalOnly.Should().ContainSingle();
        result.LocalOnly[0].MigrationId.Should().Be("20260318_NewFeature");
        result.StagingOnly.Should().BeEmpty();
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
        result.Common.Should().ContainSingle();
        result.LocalOnly.Should().BeEmpty();
        result.StagingOnly.Should().ContainSingle();
    }

    [Fact]
    public void ComputeDiff_WithEmptyLists_ReturnsAllEmpty()
    {
        var result = SchemaDiffEngine.ComputeDiff(new List<MigrationInfo>(), new List<MigrationInfo>());
        result.Common.Should().BeEmpty();
        result.LocalOnly.Should().BeEmpty();
        result.StagingOnly.Should().BeEmpty();
    }
}
