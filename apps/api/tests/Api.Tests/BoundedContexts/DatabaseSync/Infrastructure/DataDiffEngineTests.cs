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

    // ----- #4994: la riga riservata di system_configurations non partecipa al sync -----

    private static readonly string[] SystemConfigurationColumns = ["Id", "Key", "Value", "BootstrapAdminCreated"];

    [Theory]
    [InlineData("system_configurations", "Authentication:BootstrapAdmin", true)]
    [InlineData("system_configurations", "authentication:bootstrapadmin", true)]
    [InlineData("system_configurations", "  Authentication:BootstrapAdmin ", true)]
    // Spazi bianchi che btrim di Postgres non toglie: il criterio è quello di ReservedConfigurationKeys.IsReserved.
    [InlineData("system_configurations", "\tAuthentication:BootstrapAdmin\n", true)]
    [InlineData("system_configurations", "Authentication:BootstrapAdminToken", false)]
    [InlineData("system_configurations", "Registration:PublicEnabled", false)]
    // La stessa stringa in un'altra tabella è un dato qualunque.
    [InlineData("games", "Authentication:BootstrapAdmin", false)]
    public void IsExcludedRow_ExcludesOnlyTheReservedKeyOfSystemConfigurations(string table, string key, bool expected)
    {
        object?[] row = [Guid.NewGuid(), key, "true", true];

        DataDiffEngine.IsExcludedRow(table, SystemConfigurationColumns, row).Should().Be(expected);
    }

    [Fact]
    public void IsExcludedRow_WhenTheKeyColumnIsNotSynced_FailsClosedForSystemConfigurations()
    {
        // Una riga di system_configurations senza la colonna Key tra quelle lette non si può riconoscere: non si scrive.
        object?[] row = [Guid.NewGuid(), "true"];

        DataDiffEngine.IsExcludedRow("system_configurations", ["Id", "Value"], row).Should().BeTrue();
    }

    [Fact]
    public void IsExcludedRow_ForAnotherTable_NeverExcludes()
    {
        object?[] row = [Guid.NewGuid(), "x"];

        DataDiffEngine.IsExcludedRow("games", ["Id", "Name"], row).Should().BeFalse();
    }
}
