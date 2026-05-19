using Api.Services.Pdf;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="StorageLayoutOptions"/> (issue #1314 PR 2).
/// Verifies default values for Phase 0 deploy + configuration binding +
/// <see cref="StorageLayoutOptions.LayoutVersionLabel"/> derivation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Issue", "1314")]
public sealed class StorageLayoutOptionsTests
{
    [Fact]
    public void Defaults_AreSafeForPhase0Deploy()
    {
        // Phase 0 invariant: a fresh deploy must not trigger any migration
        // behavior. Defaults are Legacy write + Dual read + migration off.
        var options = new StorageLayoutOptions();

        options.WriteMode.Should().Be(StorageWriteMode.Legacy);
        options.ReadMode.Should().Be(StorageReadMode.Dual);
        options.MigrationEnabled.Should().BeFalse();
    }

    [Fact]
    public void LayoutVersionLabel_DerivesFromWriteMode()
    {
        new StorageLayoutOptions { WriteMode = StorageWriteMode.Legacy }
            .LayoutVersionLabel.Should().Be("v1-gameId");
        new StorageLayoutOptions { WriteMode = StorageWriteMode.Dual }
            .LayoutVersionLabel.Should().Be("v1-gameId-migrating");
        new StorageLayoutOptions { WriteMode = StorageWriteMode.New }
            .LayoutVersionLabel.Should().Be("v2-categorized");
    }

    [Theory]
    [InlineData("Legacy", StorageWriteMode.Legacy)]
    [InlineData("Dual", StorageWriteMode.Dual)]
    [InlineData("New", StorageWriteMode.New)]
    internal void ConfigBinding_WriteModeFromStringValue(string configValue, StorageWriteMode expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageLayout:WriteMode"] = configValue,
                ["StorageLayout:ReadMode"] = "Dual",
                ["StorageLayout:MigrationEnabled"] = "false",
            })
            .Build();

        var bound = config.GetSection(StorageLayoutOptions.SectionName).Get<StorageLayoutOptions>();
        bound.Should().NotBeNull();
        bound!.WriteMode.Should().Be(expected);
    }

    [Fact]
    public void ConfigBinding_MigrationEnabledTrue_WhenStringTrue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StorageLayout:MigrationEnabled"] = "true",
            })
            .Build();

        var bound = config.GetSection(StorageLayoutOptions.SectionName).Get<StorageLayoutOptions>();
        bound.Should().NotBeNull();
        bound!.MigrationEnabled.Should().BeTrue();
    }
}
